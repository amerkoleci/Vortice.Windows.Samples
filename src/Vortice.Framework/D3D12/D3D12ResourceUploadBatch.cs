//--------------------------------------------------------------------------------------
// File: ResourceUploadBatch.h
//
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// http://go.microsoft.com/fwlink/?LinkID=615561
//--------------------------------------------------------------------------------------

// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.
// Port of DirectXTK12 ResourceUploadBatch

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Vortice.Framework;

public sealed class D3D12ResourceUploadBatch
{
    private readonly FeatureDataD3D12Options _options;
    public readonly ID3D12Device Device;
    private CommandListType _commandType = CommandListType.Direct;
    private bool _inBeginEndBlock;
    private ID3D12CommandAllocator? _commandAllocator;
    private ID3D12GraphicsCommandList? _commandList;
    private readonly List<ID3D12DeviceChild> _trackedObjects = [];
    private GenerateMipsResources? _genMipsResources;

    public D3D12ResourceUploadBatch(ID3D12Device device)
    {
        Guard.IsNotNull(device, nameof(device));

        Device = device;
        _options = device.Options;
    }

    public void Begin(CommandListType commandType = CommandListType.Direct)
    {
        if (_inBeginEndBlock)
            throw new InvalidOperationException("Can't Begin: already in a Begin-End block.");

        switch (commandType)
        {
            case CommandListType.Direct:
            case CommandListType.Compute:
            case CommandListType.Copy:
                break;

            default:
                throw new InvalidOperationException("ResourceUploadBatch only supports Direct, Compute, and Copy command queues");
        }

        _commandAllocator = Device.CreateCommandAllocator(commandType);
        _commandAllocator.Name = "ResourceUploadBatch";
        _commandList = Device.CreateCommandList<ID3D12GraphicsCommandList>(commandType, _commandAllocator);
        _commandList.Name = "ResourceUploadBatch";

        _commandType = commandType;
        _inBeginEndBlock = true;
    }

    public void End(ID3D12CommandQueue commandQueue)
    {
        if (!_inBeginEndBlock)
            throw new InvalidOperationException("ResourceUploadBatch already closed.");

        _commandList!.Close();

        // Submit the job to the GPU
        commandQueue.ExecuteCommandList(_commandList!);

        using ID3D12Fence fence = Device.CreateFence(0);
        fence.Name = "ResourceUploadBatch";

        commandQueue.Signal(fence, 1).CheckError();
        fence.SetEventOnCompletion(1).CheckError();

        foreach (ID3D12DeviceChild resource in _trackedObjects)
        {
            resource.Dispose();
        }
        _trackedObjects.Clear();

        // Reset our state
        _commandType = CommandListType.Direct;
        _inBeginEndBlock = false;
        _commandList.Dispose(); _commandList = default;
        _commandAllocator!.Dispose(); _commandAllocator = default;

        // Swap above should have cleared these
        Debug.Assert(_trackedObjects.Count == 0);
        //assert(mTrackedMemoryResources.empty());
    }

    // Transition a resource once you're done with it
    public void Transition(ID3D12Resource resource, ResourceStates stateBefore, ResourceStates stateAfter)
    {
        if (!_inBeginEndBlock)
            throw new InvalidOperationException("Can't call Upload on a closed ResourceUploadBatch.");

        if (_commandType == CommandListType.Copy)
        {
            switch (stateAfter)
            {
                case ResourceStates.CopyDest:
                case ResourceStates.CopySource:
                    break;

                default:
                    // Ignore other states for copy queues.
                    return;
            }
        }
        else if (_commandType == CommandListType.Compute)
        {
            switch (stateAfter)
            {
                case ResourceStates.VertexAndConstantBuffer:
                case ResourceStates.UnorderedAccess:
                case ResourceStates.NonPixelShaderResource:
                case ResourceStates.IndirectArgument:
                case ResourceStates.CopyDest:
                case ResourceStates.CopySource:
                    break;

                default:
                    // Ignore other states for compute queues.
                    return;
            }
        }

        if (stateBefore == stateAfter)
            return;

        _commandList!.ResourceBarrierTransition(resource, stateBefore, stateAfter);
    }

    public unsafe void Upload(ID3D12Resource resource, uint subresourceIndexStart, SubresourceData* subRes, uint numSubresources)
    {
        if (!_inBeginEndBlock)
            throw new InvalidOperationException("Can't call Upload on a closed ResourceUploadBatch.");

        ulong uploadSize = Device.GetRequiredIntermediateSize(resource, subresourceIndexStart, numSubresources);

        ID3D12Resource scratchResource = Device.CreateCommittedResource(
            HeapType.Upload,
            HeapFlags.None,
            ResourceDescription.Buffer(uploadSize),
            ResourceStates.GenericRead
            );
        scratchResource.Name = "ResourceUploadBatch Temporary";

        _commandList!.UpdateSubresources(resource, scratchResource, 0, subresourceIndexStart, numSubresources, subRes);

        // Remember this upload object for delayed release
        _trackedObjects.Add(scratchResource);
    }

    // Asynchronously generate mips from a resource.
    // Resource must be in the PIXEL_SHADER_RESOURCE state
    public void GenerateMips(ID3D12Resource resource)
    {
        if (!_inBeginEndBlock)
            throw new InvalidOperationException("Can't call GenerateMips on a closed ResourceUploadBatch.");

        if (_commandType == CommandListType.Copy)
        {
            throw new InvalidOperationException("GenerateMips cannot operate on a copy queue");
        }

        ResourceDescription desc = resource.Description;
        if (desc.MipLevels == 1)
        {
            // Nothing to do
            return;
        }
        if (desc.MipLevels == 0)
        {
            throw new InvalidOperationException("GenerateMips: texture has no mips");
        }
        if (desc.Dimension != ResourceDimension.Texture2D)
        {
            throw new InvalidOperationException("GenerateMips only supports Texture2D resources");
        }

        if (desc.DepthOrArraySize != 1)
        {
            throw new InvalidOperationException("GenerateMips only supports 2D textures of array size 1");
        }

        bool uavCompat = FormatIsUAVCompatible(Device, _options.TypedUAVLoadAdditionalFormats, desc.Format);

        if (!uavCompat && !desc.Format.IsSRGB() && !desc.Format.IsBGR())
        {
            throw new InvalidOperationException("GenerateMips doesn't support this texture format on this device");
        }

        // Ensure that we have valid generate mips data
        _genMipsResources ??= new GenerateMipsResources(Device);

        // If the texture's format doesn't support UAVs we'll have to copy it to a texture that does first.
        // This is true of BGRA or sRGB textures, for example.
        if (uavCompat)
        {
            GenerateMips_UnorderedAccessPath(resource);
        }
        else if (!_options.TypedUAVLoadAdditionalFormats)
        {
            throw new InvalidOperationException("GenerateMips needs TypedUAVLoadAdditionalFormats device support for sRGB/BGR");
        }
        else if (desc.Format.IsBGR())
        {
            if (!_options.StandardSwizzle64KBSupported)
            {
                throw new InvalidOperationException("GenerateMips needs StandardSwizzle64KBSupported device support for BGR");
            }

            //GenerateMips_TexturePathBGR(resource);
        }
        else
        {
            GenerateMips_TexturePath(resource);
        }
    }

    private unsafe void GenerateMips_UnorderedAccessPath(ID3D12Resource resource)
    {
        ResourceDescription desc = resource.Description;
        Debug.Assert(!desc.Format.IsBGR() && !desc.Format.IsSRGB());
        Debug.Assert(_commandList is not null);

        //const CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);

        Debug.Assert(_commandType != CommandListType.Copy);
        ResourceStates originalState = (_commandType == CommandListType.Compute) ? ResourceStates.CopyDest : ResourceStates.PixelShaderResource;

        // Create a staging resource if we have to
        ID3D12Resource staging;
        if ((desc.Flags & ResourceFlags.AllowUnorderedAccess) == 0)
        {
            ResourceDescription stagingDesc = desc;
            stagingDesc.Flags |= ResourceFlags.AllowUnorderedAccess;
            stagingDesc.Format = ConvertSRVtoResourceFormat(desc.Format);

            staging = Device.CreateCommittedResource(
                HeapType.Default,
                HeapFlags.None,
                stagingDesc,
                ResourceStates.CopyDest
                );

            staging.Name = "GenerateMips Staging";

            // Copy the top mip of resource to staging
            Transition(resource, originalState, ResourceStates.CopySource);

            TextureCopyLocation src = new(resource, 0);
            TextureCopyLocation dst = new(staging, 0);
            _commandList.CopyTextureRegion(dst, 0, 0, 0, src);

            Transition(staging, ResourceStates.CopyDest, ResourceStates.NonPixelShaderResource);
        }
        else
        {
            // Resource is already a UAV so we can do this in-place
            staging = resource;

            Transition(staging, originalState, ResourceStates.NonPixelShaderResource);
        }

        // Create a descriptor heap that holds our resource descriptors
        DescriptorHeapDescription descriptorHeapDesc = new(
            DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            desc.MipLevels,
            DescriptorHeapFlags.ShaderVisible);
        ID3D12DescriptorHeap descriptorHeap = Device.CreateDescriptorHeap(descriptorHeapDesc);
        descriptorHeap.Name = "ResourceUploadBatch";
        uint descriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);

        // Create the top-level SRV
        CpuDescriptorHandle handleIt = descriptorHeap.GetCPUDescriptorHandleForHeapStart();
        ShaderResourceViewDescription srvDesc = new()
        {
            Format = desc.Format,
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Shader4ComponentMapping = ShaderComponentMapping.Default
        };
        srvDesc.Texture2D.MostDetailedMip = 0;
        srvDesc.Texture2D.MipLevels = desc.MipLevels;
        Device.CreateShaderResourceView(staging, srvDesc, handleIt);

        // Create the UAVs for the tail
        for (uint mip = 1; mip < desc.MipLevels; ++mip)
        {
            UnorderedAccessViewDescription uavDesc = new()
            {
                Format = desc.Format,
                ViewDimension = UnorderedAccessViewDimension.Texture2D
            };
            uavDesc.Texture2D.MipSlice = mip;

            handleIt.Offset((int)descriptorSize);
            Device.CreateUnorderedAccessView(staging, null, uavDesc, handleIt);
        }

        // based on format, select srgb or not
        ID3D12PipelineState pso = _genMipsResources!.GenerateMipsPSO;

        // Set up state
        _commandList.SetComputeRootSignature(_genMipsResources.RootSignature);
        _commandList.SetPipelineState(pso);
        _commandList.SetDescriptorHeaps(descriptorHeap);

        GpuDescriptorHandle handle = descriptorHeap.GetGPUDescriptorHandleForHeapStart();

        _commandList.SetComputeRootDescriptorTable((int)GenerateMipsResources.RootParameterIndex.SourceTexture, handle);

        // Get the descriptor handle -- uavH will increment over each loop
        GpuDescriptorHandle uavH = new(handle, 0, descriptorSize); // offset by 1 descriptor

        // Process each mip
        uint mipWidth = (uint)desc.Width;
        uint mipHeight = desc.Height;
        for (uint mip = 1; mip < desc.MipLevels; ++mip)
        {
            mipWidth = Math.Max(1, mipWidth >> 1);
            mipHeight = Math.Max(1, mipHeight >> 1);

            // Transition the mip to a UAV
            ResourceBarrier srv2uavDesc = ResourceBarrier.BarrierTransition(staging, ResourceStates.NonPixelShaderResource, ResourceStates.UnorderedAccess, mip);
            _commandList.ResourceBarrier(srv2uavDesc);

            // Bind the mip subresources
            _commandList.SetComputeRootDescriptorTable((uint)GenerateMipsResources.RootParameterIndex.TargetTexture, uavH);

            // Set constants
            GenerateMipsResources.ConstantData constants;
            constants.SrcMipIndex = mip - 1;
            constants.InvOutTexelSize = new(1 / (float)mipWidth, 1 / (float)mipHeight);
            _commandList.SetComputeRoot32BitConstants(
                (uint)GenerateMipsResources.RootParameterIndex.Constants,
                GenerateMipsResources.Num32BitConstants,
                &constants,
                0);

            // Process this mip
            _commandList.Dispatch(
                (mipWidth + GenerateMipsResources.ThreadGroupSize - 1) / GenerateMipsResources.ThreadGroupSize,
                (mipHeight + GenerateMipsResources.ThreadGroupSize - 1) / GenerateMipsResources.ThreadGroupSize,
                1);

            // Set up UAV barrier (used in loop)
            ResourceBarrier barrierUAV = ResourceBarrier.BarrierUnorderedAccessView(staging);
            _commandList.ResourceBarrier(barrierUAV);

            // Transition the mip to an SRV
            ResourceBarrier uav2srvDesc = ResourceBarrier.BarrierTransition(staging, ResourceStates.UnorderedAccess, ResourceStates.NonPixelShaderResource, mip);
            _commandList.ResourceBarrier(uav2srvDesc);

            // Offset the descriptor heap handles
            uavH.Offset((int)descriptorSize);
        }

        // If the staging resource is NOT the same as the resource, we need to copy everything back
        if (staging != resource)
        {
            // Transition the resources ready for copy
            Span<ResourceBarrier> barriers = new ResourceBarrier[2];
            barriers[0] = ResourceBarrier.BarrierTransition(staging, ResourceStates.NonPixelShaderResource, ResourceStates.CopySource);
            barriers[1] = ResourceBarrier.BarrierTransition(resource, ResourceStates.CopySource, ResourceStates.CopyDest);
            _commandList.ResourceBarrier(barriers);

            // Copy the entire resource back
            _commandList.CopyResource(resource, staging);

            // Transition the target resource back to pixel shader resource
            Transition(resource, ResourceStates.CopyDest, originalState);

            _trackedObjects.Add(staging);
        }
        else
        {
            Transition(staging, ResourceStates.NonPixelShaderResource, originalState);
        }

        // Add our temporary objects to the deferred deletion queue
        _trackedObjects.Add(_genMipsResources.RootSignature);
        _trackedObjects.Add(pso);
        if (staging != resource)
        {
            _trackedObjects.Add(staging);
        }
        _trackedObjects.Add(descriptorHeap);
    }

    private void GenerateMips_TexturePath(ID3D12Resource resource)
    {
        Debug.Assert(_commandList is not null);
        Debug.Assert(_commandType != CommandListType.Copy);
        ResourceDescription resourceDesc = resource.Description;
        Debug.Assert(!resourceDesc.Format.IsBGR() || resourceDesc.Format.IsSRGB());

        ResourceDescription copyDesc = resourceDesc;
        copyDesc.Format = Format.R8G8B8A8_UNorm;
        copyDesc.Flags |= ResourceFlags.AllowUnorderedAccess;

        // Create a resource with the same description, but without SRGB, and with UAV flags
        ID3D12Resource resourceCopy = Device.CreateCommittedResource(
            HeapType.Default,
            HeapFlags.None,
            copyDesc,
            ResourceStates.CopyDest
            );

        resourceCopy.Name = "GenerateMips Resource Copy";

        ResourceStates originalState = _commandType == CommandListType.Compute
            ? ResourceStates.CopyDest : ResourceStates.PixelShaderResource;

        // Copy the top mip of resource data
        Transition(resource, originalState, ResourceStates.CopySource);

        TextureCopyLocation src = new(resource, 0);
        TextureCopyLocation dst = new(resourceCopy, 0);
        _commandList.CopyTextureRegion(dst, 0, 0, 0, src);

        Transition(resourceCopy, ResourceStates.CopyDest, originalState);

        // Generate the mips
        GenerateMips_UnorderedAccessPath(resourceCopy);

        // Direct copy back
        Span<ResourceBarrier> barriers =
        [
            ResourceBarrier.BarrierTransition(resourceCopy, originalState, ResourceStates.CopySource),
            ResourceBarrier.BarrierTransition(resource, ResourceStates.CopySource, ResourceStates.CopyDest)
        ];
        _commandList.ResourceBarrier(barriers);

        // Copy the entire resource back
        _commandList.CopyResource(resource, resourceCopy);

        Transition(resource, ResourceStates.CopyDest, originalState);

        // Track these object lifetimes on the GPU
        _trackedObjects.Add(resourceCopy);
        //_trackedObjects.Add(resource); 
    }

    public bool IsSupportedForGenerateMips(Format format)
    {
        if (_commandType == CommandListType.Copy)
            return false;

        if (FormatIsUAVCompatible(Device, _options.TypedUAVLoadAdditionalFormats, format))
            return true;

        if (format.IsBGR())
        {
            // BGR path requires DXGI_FORMAT_R8G8B8A8_UNORM support for UAV load/store plus matching layouts
            return _options.TypedUAVLoadAdditionalFormats && _options.StandardSwizzle64KBSupported;
        }

        if (format.IsSRGB())
        {
            // sRGB path requires DXGI_FORMAT_R8G8B8A8_UNORM support for UAV load/store
            return _options.TypedUAVLoadAdditionalFormats;
        }

        return false;
    }

    private static bool FormatIsUAVCompatible(ID3D12Device device, bool typedUAVLoadAdditionalFormats, Format format)
    {
        switch (format)
        {
            case Format.R32_Float:
            case Format.R32_UInt:
            case Format.R32_SInt:
                // Unconditionally supported.
                return true;

            case Format.R32G32B32A32_Float:
            case Format.R32G32B32A32_UInt:
            case Format.R32G32B32A32_SInt:
            case Format.R16G16B16A16_Float:
            case Format.R16G16B16A16_UInt:
            case Format.R16G16B16A16_SInt:
            case Format.R8G8B8A8_UNorm:
            case Format.R8G8B8A8_UInt:
            case Format.R8G8B8A8_SInt:
            case Format.R16_Float:
            case Format.R16_UInt:
            case Format.R16_SInt:
            case Format.R8_UNorm:
            case Format.R8_UInt:
            case Format.R8_SInt:
                // All these are supported if this optional feature is set.
                return typedUAVLoadAdditionalFormats;

            case Format.R16G16B16A16_UNorm:
            case Format.R16G16B16A16_SNorm:
            case Format.R32G32_Float:
            case Format.R32G32_UInt:
            case Format.R32G32_SInt:
            case Format.R10G10B10A2_UNorm:
            case Format.R10G10B10A2_UInt:
            case Format.R11G11B10_Float:
            case Format.R8G8B8A8_SNorm:
            case Format.R16G16_Float:
            case Format.R16G16_UNorm:
            case Format.R16G16_UInt:
            case Format.R16G16_SNorm:
            case Format.R16G16_SInt:
            case Format.R8G8_UNorm:
            case Format.R8G8_UInt:
            case Format.R8G8_SNorm:
            case Format.R8G8_SInt:
            case Format.R16_UNorm:
            case Format.R16_SNorm:
            case Format.R8_SNorm:
            case Format.A8_UNorm:
            case Format.B5G6R5_UNorm:
            case Format.B5G5R5A1_UNorm:
            case Format.B4G4R4A4_UNorm:
                // Conditionally supported by specific devices.
                if (typedUAVLoadAdditionalFormats)
                {
                    if (device.CheckFormatSupport(format, out FormatSupport1 formatSupport1, out FormatSupport2 formatSupport2))
                    {
                        FormatSupport2 mask = FormatSupport2.UnorderedAccessViewTypedLoad | FormatSupport2.UnorderedAccessViewTypedStore;
                        return ((formatSupport2 & mask) == mask);
                    }
                }
                return false;

            default:
                return false;
        }
    }

    private static Format ConvertSRVtoResourceFormat(Format format)
    {
        switch (format)
        {
            case Format.R32G32B32A32_Float:
            case Format.R32G32B32A32_UInt:
            case Format.R32G32B32A32_SInt:
                return Format.R32G32B32A32_Typeless;

            case Format.R16G16B16A16_Float:
            case Format.R16G16B16A16_UNorm:
            case Format.R16G16B16A16_UInt:
            case Format.R16G16B16A16_SNorm:
            case Format.R16G16B16A16_SInt:
                return Format.R16G16B16A16_Typeless;

            case Format.R32G32_Float:
            case Format.R32G32_UInt:
            case Format.R32G32_SInt:
                return Format.R32G32_Typeless;

            case Format.R10G10B10A2_UNorm:
            case Format.R10G10B10A2_UInt:
                return Format.R10G10B10A2_Typeless;

            case Format.R8G8B8A8_UNorm:
            case Format.R8G8B8A8_UInt:
            case Format.R8G8B8A8_SNorm:
            case Format.R8G8B8A8_SInt:
                return Format.R8G8B8A8_Typeless;

            case Format.R16G16_Float:
            case Format.R16G16_UNorm:
            case Format.R16G16_UInt:
            case Format.R16G16_SNorm:
            case Format.R16G16_SInt:
                return Format.R16G16_Typeless;

            case Format.R32_Float:
            case Format.R32_UInt:
            case Format.R32_SInt:
                return Format.R32_Typeless;

            case Format.R8G8_UNorm:
            case Format.R8G8_UInt:
            case Format.R8G8_SNorm:
            case Format.R8G8_SInt:
                return Format.R8G8_Typeless;

            case Format.R16_Float:
            case Format.R16_UNorm:
            case Format.R16_UInt:
            case Format.R16_SNorm:
            case Format.R16_SInt:
                return Format.R16_Typeless;

            case Format.R8_UNorm:
            case Format.R8_UInt:
            case Format.R8_SNorm:
            case Format.R8_SInt:
                return Format.R8_Typeless;

            default:
                return format;
        }
    }

    unsafe class GenerateMipsResources
    {
        private readonly ID3D12Device _device;
        public readonly ID3D12RootSignature RootSignature;
        public readonly ID3D12PipelineState GenerateMipsPSO;

        public static uint Num32BitConstants => (uint)sizeof(ConstantData) / sizeof(uint);
        public const int ThreadGroupSize = 8;

        #region Bytecode
        public static byte[] GenerateMipsBytecode => [
            68, 88, 66, 67, 145, 28, 144, 166, 89, 15, 96, 67, 230, 22, 237, 237, 0, 38, 12, 156, 1, 0, 0, 0, 56, 9, 0, 0, 7, 0, 0, 0, 60, 0, 0, 0, 76, 0, 0, 0, 92, 0, 0, 0, 108, 0, 0, 0, 36, 1, 0, 0, 232, 1, 0, 0, 4, 2, 0, 0, 83, 70, 73, 48, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 73, 83, 71, 49, 8, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 79, 83, 71, 49, 8, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 80, 83, 86, 48, 176, 0, 0, 0, 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 4, 0, 0, 0, 24, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 13, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 14, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 0, 0, 0, 82, 84, 83, 48, 188, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 24, 0, 0, 0, 1, 0, 0, 0, 136, 0, 0, 0, 62, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 72, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 80, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0, 112, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 20, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0, 0, 4, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 255, 255, 127, 127, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 72, 65, 83, 72, 20, 0, 0, 0, 0, 0, 0, 0, 186, 247, 129, 247, 85, 239, 239, 202, 76, 80, 78, 160, 156, 27, 111, 71, 68, 88, 73, 76, 44, 7, 0, 0, 100, 0, 5, 0, 203, 1, 0, 0, 68, 88, 73, 76, 4, 1, 0, 0, 16, 0, 0, 0, 20, 7, 0, 0, 66, 67, 192, 222, 33, 12, 0, 0, 194, 1, 0, 0, 11, 130, 32, 0, 2, 0, 0, 0, 19, 0, 0, 0, 7, 129, 35, 145, 65, 200, 4, 73, 6, 16, 50, 57, 146, 1, 132, 12, 37, 5, 8, 25, 30, 4, 139, 98, 128, 24, 69, 2, 66, 146, 11, 66, 196, 16, 50, 20, 56, 8, 24, 75, 10, 50, 98, 136, 72, 144, 20, 32, 67, 70, 136, 165, 0, 25, 50, 66, 228, 72, 14, 144, 17, 35, 196, 80, 65, 81, 129, 140, 225, 131, 229, 138, 4, 49, 70, 6, 81, 24, 0, 0, 8, 0, 0, 0, 27, 140, 224, 255, 255, 255, 255, 7, 64, 2, 168, 13, 132, 240, 255, 255, 255, 255, 3, 32, 109, 48, 134, 255, 255, 255, 255, 31, 0, 9, 168, 0, 73, 24, 0, 0, 3, 0, 0, 0, 19, 130, 96, 66, 32, 76, 8, 6, 0, 0, 0, 0, 137, 32, 0, 0, 101, 0, 0, 0, 50, 34, 136, 9, 32, 100, 133, 4, 19, 35, 164, 132, 4, 19, 35, 227, 132, 161, 144, 20, 18, 76, 140, 140, 11, 132, 196, 76, 16, 144, 193, 8, 64, 9, 0, 10, 230, 8, 192, 160, 12, 195, 48, 16, 49, 71, 128, 144, 113, 207, 112, 249, 19, 246, 16, 146, 31, 2, 205, 176, 16, 40, 56, 102, 0, 202, 2, 12, 200, 48, 12, 73, 146, 36, 6, 41, 55, 13, 151, 63, 97, 15, 33, 249, 43, 33, 173, 196, 228, 23, 183, 141, 138, 36, 73, 146, 161, 48, 204, 128, 32, 73, 146, 36, 195, 48, 36, 212, 28, 53, 92, 254, 132, 61, 132, 228, 115, 27, 85, 172, 196, 228, 35, 183, 141, 136, 97, 24, 134, 66, 60, 3, 50, 16, 116, 212, 112, 249, 19, 246, 16, 146, 207, 109, 84, 177, 18, 147, 95, 220, 54, 34, 146, 36, 73, 10, 33, 13, 200, 64, 211, 28, 65, 80, 12, 100, 48, 134, 161, 34, 107, 32, 96, 24, 129, 72, 102, 106, 131, 113, 96, 135, 112, 152, 135, 121, 112, 3, 90, 40, 7, 124, 160, 135, 122, 144, 135, 114, 144, 3, 82, 224, 3, 123, 40, 135, 113, 160, 135, 119, 144, 7, 62, 48, 7, 118, 120, 135, 112, 160, 7, 54, 0, 3, 58, 240, 3, 48, 240, 3, 61, 208, 131, 118, 72, 7, 120, 152, 135, 95, 160, 135, 124, 128, 135, 114, 64, 193, 48, 147, 24, 140, 3, 59, 132, 195, 60, 204, 131, 27, 208, 66, 57, 224, 3, 61, 212, 131, 60, 148, 131, 28, 144, 2, 31, 216, 67, 57, 140, 3, 61, 188, 131, 60, 240, 129, 57, 176, 195, 59, 132, 3, 61, 176, 1, 24, 208, 129, 31, 128, 129, 31, 32, 33, 211, 104, 155, 137, 12, 198, 129, 29, 194, 97, 30, 230, 193, 13, 100, 225, 22, 104, 161, 28, 240, 129, 30, 234, 65, 30, 202, 65, 14, 72, 129, 15, 236, 161, 28, 198, 129, 30, 222, 65, 30, 248, 192, 28, 216, 225, 29, 194, 129, 30, 216, 0, 12, 232, 192, 15, 192, 192, 15, 80, 144, 81, 55, 140, 32, 36, 199, 152, 200, 195, 57, 141, 52, 1, 205, 36, 33, 225, 27, 8, 188, 73, 154, 34, 74, 152, 124, 22, 96, 158, 133, 136, 216, 9, 152, 8, 20, 12, 36, 2, 19, 20, 114, 192, 135, 116, 96, 135, 54, 104, 135, 121, 104, 3, 114, 192, 135, 13, 175, 80, 14, 109, 208, 14, 122, 80, 14, 109, 0, 15, 122, 48, 7, 114, 160, 7, 115, 32, 7, 109, 144, 14, 113, 160, 7, 115, 32, 7, 109, 144, 14, 120, 160, 7, 115, 32, 7, 109, 144, 14, 113, 96, 7, 122, 48, 7, 114, 208, 6, 233, 48, 7, 114, 160, 7, 115, 32, 7, 109, 144, 14, 118, 64, 7, 122, 96, 7, 116, 208, 6, 230, 16, 7, 118, 160, 7, 115, 32, 7, 109, 96, 14, 115, 32, 7, 122, 48, 7, 114, 208, 6, 230, 96, 7, 116, 160, 7, 118, 64, 7, 109, 224, 14, 120, 160, 7, 113, 96, 7, 122, 48, 7, 114, 160, 7, 118, 64, 7, 67, 158, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 134, 60, 4, 16, 0, 1, 0, 0, 0, 0, 0, 0, 0, 12, 121, 20, 32, 0, 4, 0, 0, 0, 0, 0, 0, 0, 24, 242, 52, 64, 0, 12, 0, 0, 0, 0, 0, 0, 0, 48, 228, 129, 128, 0, 24, 0, 0, 0, 0, 0, 0, 0, 96, 200, 51, 1, 1, 48, 0, 0, 0, 0, 0, 0, 0, 192, 144, 199, 2, 2, 96, 0, 0, 0, 0, 0, 0, 0, 128, 44, 16, 12, 0, 0, 0, 50, 30, 152, 20, 25, 17, 76, 144, 140, 9, 38, 71, 198, 4, 67, 26, 74, 160, 16, 10, 162, 24, 70, 0, 138, 160, 36, 10, 131, 170, 17, 0, 226, 102, 0, 200, 155, 1, 160, 112, 6, 128, 198, 25, 0, 0, 0, 0, 121, 24, 0, 0, 68, 0, 0, 0, 26, 3, 76, 144, 70, 2, 19, 68, 143, 12, 111, 236, 237, 77, 12, 36, 198, 5, 199, 69, 134, 6, 166, 198, 37, 166, 6, 4, 197, 140, 236, 166, 172, 134, 70, 108, 140, 44, 101, 67, 16, 76, 16, 134, 99, 130, 48, 32, 27, 132, 129, 152, 32, 12, 201, 6, 97, 48, 40, 140, 205, 77, 16, 6, 101, 195, 128, 36, 196, 4, 97, 88, 38, 8, 28, 68, 96, 130, 48, 48, 19, 132, 161, 217, 32, 44, 207, 134, 100, 97, 154, 101, 25, 156, 5, 218, 16, 68, 19, 4, 47, 154, 32, 84, 207, 134, 101, 153, 154, 101, 25, 28, 138, 162, 160, 13, 65, 53, 65, 8, 3, 105, 130, 48, 56, 27, 144, 229, 106, 150, 101, 192, 128, 13, 65, 54, 65, 24, 131, 105, 3, 178, 108, 205, 178, 12, 11, 176, 33, 224, 54, 16, 146, 165, 117, 19, 4, 1, 32, 209, 22, 150, 230, 182, 97, 48, 140, 97, 131, 64, 132, 193, 134, 226, 3, 3, 192, 19, 131, 42, 108, 108, 118, 109, 46, 105, 100, 101, 110, 116, 83, 130, 160, 10, 25, 158, 139, 93, 153, 220, 92, 218, 155, 219, 148, 128, 104, 66, 134, 231, 98, 23, 198, 102, 87, 38, 55, 37, 48, 234, 144, 225, 185, 204, 161, 133, 145, 149, 201, 53, 189, 145, 149, 177, 77, 9, 146, 50, 100, 120, 46, 114, 101, 115, 111, 117, 114, 99, 101, 115, 83, 130, 174, 14, 25, 158, 75, 153, 27, 157, 92, 30, 212, 91, 154, 27, 221, 220, 148, 64, 12, 0, 121, 24, 0, 0, 76, 0, 0, 0, 51, 8, 128, 28, 196, 225, 28, 102, 20, 1, 61, 136, 67, 56, 132, 195, 140, 66, 128, 7, 121, 120, 7, 115, 152, 113, 12, 230, 0, 15, 237, 16, 14, 244, 128, 14, 51, 12, 66, 30, 194, 193, 29, 206, 161, 28, 102, 48, 5, 61, 136, 67, 56, 132, 131, 27, 204, 3, 61, 200, 67, 61, 140, 3, 61, 204, 120, 140, 116, 112, 7, 123, 8, 7, 121, 72, 135, 112, 112, 7, 122, 112, 3, 118, 120, 135, 112, 32, 135, 25, 204, 17, 14, 236, 144, 14, 225, 48, 15, 110, 48, 15, 227, 240, 14, 240, 80, 14, 51, 16, 196, 29, 222, 33, 28, 216, 33, 29, 194, 97, 30, 102, 48, 137, 59, 188, 131, 59, 208, 67, 57, 180, 3, 60, 188, 131, 60, 132, 3, 59, 204, 240, 20, 118, 96, 7, 123, 104, 7, 55, 104, 135, 114, 104, 7, 55, 128, 135, 112, 144, 135, 112, 96, 7, 118, 40, 7, 118, 248, 5, 118, 120, 135, 119, 128, 135, 95, 8, 135, 113, 24, 135, 114, 152, 135, 121, 152, 129, 44, 238, 240, 14, 238, 224, 14, 245, 192, 14, 236, 48, 3, 98, 200, 161, 28, 228, 161, 28, 204, 161, 28, 228, 161, 28, 220, 97, 28, 202, 33, 28, 196, 129, 29, 202, 97, 6, 214, 144, 67, 57, 200, 67, 57, 152, 67, 57, 200, 67, 57, 184, 195, 56, 148, 67, 56, 136, 3, 59, 148, 195, 47, 188, 131, 60, 252, 130, 59, 212, 3, 59, 176, 195, 12, 196, 33, 7, 124, 112, 3, 122, 40, 135, 118, 128, 135, 25, 209, 67, 14, 248, 224, 6, 228, 32, 14, 231, 224, 6, 246, 16, 14, 242, 192, 14, 225, 144, 15, 239, 80, 15, 244, 0, 0, 0, 113, 32, 0, 0, 30, 0, 0, 0, 86, 176, 13, 151, 239, 60, 190, 16, 80, 69, 65, 68, 165, 3, 12, 37, 97, 0, 2, 230, 23, 183, 109, 4, 219, 112, 249, 206, 227, 11, 1, 85, 20, 68, 84, 58, 192, 80, 18, 6, 32, 96, 62, 114, 219, 102, 32, 13, 151, 239, 60, 190, 16, 17, 192, 68, 132, 64, 51, 44, 132, 13, 84, 195, 229, 59, 143, 47, 1, 204, 179, 16, 37, 81, 17, 139, 95, 220, 182, 9, 88, 195, 229, 59, 143, 63, 17, 215, 68, 69, 4, 59, 57, 17, 225, 23, 183, 109, 1, 210, 112, 249, 206, 227, 79, 71, 68, 0, 131, 56, 248, 200, 109, 27, 0, 193, 0, 72, 3, 0, 97, 32, 0, 0, 56, 0, 0, 0, 19, 4, 65, 44, 16, 0, 0, 0, 9, 0, 0, 0, 52, 148, 92, 233, 6, 148, 221, 12, 64, 241, 149, 97, 0, 25, 37, 48, 2, 80, 6, 69, 80, 30, 148, 140, 17, 128, 32, 8, 194, 223, 12, 0, 0, 0, 0, 0, 35, 6, 9, 0, 130, 96, 96, 109, 205, 97, 89, 210, 136, 65, 2, 128, 32, 24, 88, 156, 115, 92, 215, 52, 98, 144, 0, 32, 8, 6, 86, 247, 28, 24, 70, 141, 24, 36, 0, 8, 130, 129, 229, 65, 71, 150, 85, 35, 6, 6, 0, 130, 96, 64, 144, 1, 164, 141, 24, 24, 0, 8, 130, 1, 81, 6, 209, 119, 66, 82, 39, 36, 101, 130, 2, 31, 19, 22, 248, 140, 24, 28, 0, 8, 130, 193, 100, 6, 213, 1, 6, 163, 9, 1, 48, 154, 32, 4, 38, 20, 242, 177, 66, 144, 207, 136, 193, 1, 128, 32, 24, 64, 108, 160, 49, 101, 48, 154, 16, 8, 23, 36, 53, 98, 240, 0, 32, 8, 6, 13, 28, 108, 17, 84, 16, 211, 132, 6, 104, 192, 5, 163, 9, 1, 48, 154, 32, 4, 163, 9, 131, 48, 154, 64, 12, 35, 6, 14, 0, 130, 96, 160, 216, 193, 119, 77, 82, 24, 16, 131, 16, 104, 8, 0, 0, 0, 0, 0, 0, 0
        ];
        #endregion

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct ConstantData
        {
            public Vector2 InvOutTexelSize;
            public uint SrcMipIndex;
            private uint _pading;
        };

        public GenerateMipsResources(ID3D12Device device)
        {
            _device = device;

            // CreateGenMipsRootSignature
            RootSignatureFlags rootSignatureFlags = RootSignatureFlags.DenyVertexShaderRootAccess
                | RootSignatureFlags.DenyHullShaderRootAccess
                | RootSignatureFlags.DenyDomainShaderRootAccess
                | RootSignatureFlags.DenyGeometryShaderRootAccess
                | RootSignatureFlags.DenyPixelShaderRootAccess;

            StaticSamplerDescription sampler = new(
                0,
                Filter.MinMagLinearMipPoint,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                TextureAddressMode.Clamp,
                0,
                16,
                ComparisonFunction.LessEqual,
                StaticBorderColor.OpaqueWhite, 0.0f, float.MaxValue,
                ShaderVisibility.All);

            DescriptorRange sourceDescriptorRange = new(DescriptorRangeType.ShaderResourceView, 1, 0);
            DescriptorRange targetDescriptorRange = new(DescriptorRangeType.UnorderedAccessView, 1, 0);

            RootParameter[] rootParameters =
            [
                new RootParameter(new RootConstants(0, 0, Num32BitConstants), ShaderVisibility.All),
                new RootParameter(new RootDescriptorTable(sourceDescriptorRange), ShaderVisibility.All),
                new RootParameter(new RootDescriptorTable(targetDescriptorRange), ShaderVisibility.All),
            ];
            RootSignature = device.CreateRootSignature(
                new RootSignatureDescription(rootSignatureFlags, rootParameters,
                [
                    sampler
                ]), RootSignatureVersion.Version10
            );


            ComputePipelineStateDescription csoDesc = new()
            {
                RootSignature = RootSignature,
                ComputeShader = GenerateMipsBytecode
            };
            GenerateMipsPSO = device.CreateComputePipelineState<ID3D12PipelineState>(csoDesc);
        }

        public enum RootParameterIndex
        {
            Constants,
            SourceTexture,
            TargetTexture,
            Count
        };
    }

}
