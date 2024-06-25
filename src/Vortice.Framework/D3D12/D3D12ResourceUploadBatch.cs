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
using CommunityToolkit.Diagnostics;
using Vortice.Direct3D12;

namespace Vortice.Framework;

public sealed class D3D12ResourceUploadBatch
{
    private readonly FeatureDataD3D12Options _options;
    public readonly ID3D12Device Device;
    private CommandListType _commandType = CommandListType.Direct;
    private bool _inBeginEndBlock;
    private ID3D12CommandAllocator? _commandAllocator;
    private ID3D12GraphicsCommandList? _commandList;
    private readonly List<ID3D12Resource> _trackedObjects = [];

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

        foreach(ID3D12Resource resource in _trackedObjects)
        {
            resource.Dispose();
        }
        _trackedObjects.Clear();

        // Reset our state
        _commandType = CommandListType.Direct;
        _inBeginEndBlock = false;
        _commandList.Dispose(); _commandList = default;
        _commandAllocator.Dispose(); _commandAllocator = default;

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

    public unsafe void Upload(ID3D12Resource resource, int subresourceIndexStart,
        SubresourceData* subRes, int numSubresources)
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
}
