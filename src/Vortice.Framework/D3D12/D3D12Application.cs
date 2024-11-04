// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using Vortice.Mathematics;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;

namespace Vortice.Framework;

/// <summary>
/// Class that handles all logic to have D3D12 running application.
/// </summary>
public abstract class D3D12Application : Application
{
    private readonly Format _colorFormat;
    private readonly Format _depthStencilFormat;
    private readonly FeatureLevel _minFeatureLevel;
    private readonly bool _dxgiDebug;
    private IDXGIFactory4 _dxgiFactory;
    private readonly bool _isTearingSupported;

    private readonly ulong[] _fenceValues;
    private readonly ID3D12Fence _frameFence;
    private readonly AutoResetEvent _frameFenceEvent;

    private readonly ID3D12Resource[] _renderTargets;
    private readonly uint[] _renderTargetDescriptorIndexes;
    private uint _depthStencilViewDescriptorIndex = ~0u;

    private readonly ID3D12CommandAllocator[] _commandAllocators;

    protected D3D12Application(AppPlatform? platform = default,
        Format colorFormat = Format.B8G8R8A8_UNorm,
        Format depthStencilFormat = Format.D32_Float,
        FeatureLevel minFeatureLevel = FeatureLevel.Level_11_0)
        : base(platform)
    {
        _colorFormat = colorFormat;
        _depthStencilFormat = depthStencilFormat;
        _minFeatureLevel = minFeatureLevel;

#if DEBUG
        // Enable the debug layer (requires the Graphics Tools "optional feature").
        //
        // NOTE: Enabling the debug layer after device creation will invalidate the active device.
        {
            if (D3D12GetDebugInterface(out ID3D12Debug? debugController).Success)
            {
                debugController!.EnableDebugLayer();
                debugController.Dispose();
            }
            else
            {
                Debug.WriteLine("WARNING: Direct3D Debug Device is not available");
            }

            if (DXGIGetDebugInterface1(out IDXGIInfoQueue? dxgiInfoQueue).Success)
            {
                _dxgiDebug = true;

                dxgiInfoQueue!.SetBreakOnSeverity(DebugAll, InfoQueueMessageSeverity.Error, true);
                dxgiInfoQueue!.SetBreakOnSeverity(DebugAll, InfoQueueMessageSeverity.Corruption, true);

                int[] hide = new[]
                {
                    80 /* IDXGISwapChain::GetContainingOutput: The swapchain's adapter does not control the output on which the swapchain's window resides. */,
                };
                DXGI.Debug.InfoQueueFilter filter = new();
                filter.DenyList = new DXGI.Debug.InfoQueueFilterDescription
                {
                    Ids = hide
                };
                dxgiInfoQueue!.AddStorageFilterEntries(DebugDxgi, filter);
                dxgiInfoQueue.Dispose();

            }
        }
#endif

        _dxgiFactory = CreateDXGIFactory2<IDXGIFactory4>(_dxgiDebug);

        using (IDXGIFactory5? factory5 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                _isTearingSupported = factory5.PresentAllowTearing;
            }
        }

        ID3D12Device2? device = default;
        using (IDXGIFactory6? factory6 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory6>())
        {
            if (factory6 != null)
            {
                for (uint adapterIndex = 0; factory6.EnumAdapterByGpuPreference(adapterIndex, GpuPreference.HighPerformance, out IDXGIAdapter1? adapter).Success; adapterIndex++)
                {
                    AdapterDescription1 desc = adapter!.Description1;

                    // Don't select the Basic Render Driver adapter.
                    if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    {
                        adapter.Dispose();

                        continue;
                    }

                    if (D3D12CreateDevice(adapter, _minFeatureLevel, out device).Success)
                    {
                        adapter.Dispose();

                        break;
                    }
                }
            }
            else
            {
                for (uint adapterIndex = 0;
                    _dxgiFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter).Success;
                    adapterIndex++)
                {
                    AdapterDescription1 desc = adapter.Description1;

                    // Don't select the Basic Render Driver adapter.
                    if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    {
                        adapter.Dispose();

                        continue;
                    }

                    if (D3D12CreateDevice(adapter, _minFeatureLevel, out device).Success)
                    {
                        break;
                    }
                }
            }
        }

        Device = device!;

#if DEBUG
        // Configure debug device (if active).
        {
            using ID3D12InfoQueue? d3dInfoQueue = Device.QueryInterfaceOrNull<ID3D12InfoQueue>();
            if (d3dInfoQueue != null)
            {
                d3dInfoQueue!.SetBreakOnSeverity(MessageSeverity.Corruption, true);
                d3dInfoQueue!.SetBreakOnSeverity(MessageSeverity.Error, true);
                MessageId[] hide = new[]
                {
                    MessageId.MapInvalidNullRange,
                    MessageId.UnmapInvalidNullRange,
                    // Workarounds for debug layer issues on hybrid-graphics systems
                    MessageId.ExecuteCommandListsWrongSwapChainBufferReference,
                    MessageId.ResourceBarrierMismatchingCommandListType,
                };

                Direct3D12.Debug.InfoQueueFilter filter = new();
                filter.DenyList = new Direct3D12.Debug.InfoQueueFilterDescription
                {
                    Ids = hide
                };
                d3dInfoQueue.AddStorageFilterEntries(filter);
            }
        }
#endif

        FeatureLevel = Device.CheckMaxSupportedFeatureLevel();

        // Create Command queue.
        DirectQueue = Device.CreateCommandQueue(CommandListType.Direct);
        DirectQueue.Name = "Direct Queue";

        // Create Descriptor Allocator
        // Init CPU descriptor allocators
        const int renderTargetViewHeapSize = 1024;
        const int depthStencilViewHeapSize = 256;

        // Maximum number of CBV/SRV/UAV descriptors in heap for Tier 1
        const int shaderResourceViewHeapSize = 1_000_000;
        // Maximum number of samplers descriptors in heap for Tier 1
        const int samplerHeapSize = 2048; // 2048 ->  Tier1 limit

        RenderTargetViewHeap = new(Device, DescriptorHeapType.RenderTargetView, renderTargetViewHeapSize);
        DepthStencilViewHeap = new(Device, DescriptorHeapType.DepthStencilView, depthStencilViewHeapSize);
        ShaderResourceViewHeap = new(Device, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, shaderResourceViewHeapSize);
        SamplerHeap = new(Device, DescriptorHeapType.Sampler, samplerHeapSize);

        // Create synchronization objects.
        _fenceValues = new ulong[MainWindow.BackBufferCount];
        _frameFence = Device.CreateFence(_fenceValues[0]);
        _frameFence.Name = "Frame Fence";
        _frameFenceEvent = new AutoResetEvent(false);

        // Create frame data
        _renderTargets = new ID3D12Resource[MainWindow.BackBufferCount];
        _renderTargetDescriptorIndexes = new uint[MainWindow.BackBufferCount];
        CreateWindowSizeDependentResources();

        _commandAllocators = new ID3D12CommandAllocator[MainWindow.BackBufferCount];
        for (int i = 0; i < _commandAllocators.Length; i++)
        {
            _commandAllocators[i] = Device.CreateCommandAllocator(CommandListType.Direct);
        }

        // Create a command list for recording graphics commands.
        CommandList = Device.CreateCommandList<ID3D12GraphicsCommandList4>(CommandListType.Direct, _commandAllocators[0]);
        CommandList.Close();

        // Create UploadBatch
        UploadBatch = new D3D12ResourceUploadBatch(Device);
    }

    public ID3D12Device2 Device { get; }
    public FeatureLevel FeatureLevel { get; }

    public ID3D12CommandQueue DirectQueue { get; }

    public D3D12DescriptorAllocator RenderTargetViewHeap { get; }
    public D3D12DescriptorAllocator DepthStencilViewHeap { get; }
    public D3D12DescriptorAllocator ShaderResourceViewHeap { get; }
    public D3D12DescriptorAllocator SamplerHeap { get; }

    public IDXGISwapChain3 SwapChain { get; private set; }
    public uint BackBufferIndex { get; private set; }

    public Format ColorFormat => _colorFormat;
    public ColorSpaceType ColorSpace { get; private set; } = ColorSpaceType.RgbFullG22NoneP709;
    public ID3D12Resource ColorTexture => _renderTargets[BackBufferIndex];
    public CpuDescriptorHandle ColorTextureView
    {
        get => RenderTargetViewHeap.GetCpuHandle(_renderTargetDescriptorIndexes[BackBufferIndex]);
    }

    public Format DepthStencilFormat => _depthStencilFormat;
    public ID3D12Resource? DepthStencilTexture { get; private set; }
    public CpuDescriptorHandle? DepthStencilView
    {
        get => DepthStencilViewHeap.GetCpuHandle(_depthStencilViewDescriptorIndex);
    }

    public ID3D12GraphicsCommandList4 CommandList { get; }
    public ID3D12CommandAllocator CommandAllocator => _commandAllocators[BackBufferIndex];

    /// <summary>
    /// Gets the viewport.
    /// </summary>
    public Viewport Viewport => new(MainWindow.ClientSize.Width, MainWindow.ClientSize.Height);

    public bool UseRenderPass { get; set; }

    public D3D12ResourceUploadBatch UploadBatch { get; }

    protected virtual void OnDestroy()
    {

    }

    protected sealed override void OnShutdown()
    {
        WaitForGpu();

        OnDestroy();

        DepthStencilTexture?.Dispose();

        RenderTargetViewHeap.Dispose();
        DepthStencilViewHeap.Dispose();
        ShaderResourceViewHeap.Dispose();
        SamplerHeap.Dispose();

        for (int i = 0; i < _commandAllocators.Length; i++)
        {
            _commandAllocators[i].Dispose();
            _renderTargets[i].Dispose();
        }
        CommandList.Dispose();

        _frameFence.Dispose();
        //UploadBatch.Dispose();

        SwapChain.Dispose();
        DirectQueue.Dispose();

#if DEBUG
        uint refCount = Device.Release();
        if (refCount > 0)
        {
            Debug.WriteLine($"Direct3D12: There are {refCount} unreleased references left on the device");

            ID3D12DebugDevice? d3d12DebugDevice = Device.QueryInterfaceOrNull<ID3D12DebugDevice>();
            if (d3d12DebugDevice != null)
            {
                d3d12DebugDevice.ReportLiveDeviceObjects(ReportLiveDeviceObjectFlags.Detail | ReportLiveDeviceObjectFlags.IgnoreInternal);
                d3d12DebugDevice.Dispose();
            }
        }
#else
        Device.Dispose();
#endif

        _dxgiFactory.Dispose();

#if DEBUG
        if (DXGIGetDebugInterface1(out IDXGIDebug1? dxgiDebug).Success)
        {
            dxgiDebug!.ReportLiveObjects(DebugAll, ReportLiveObjectFlags.Summary | ReportLiveObjectFlags.IgnoreInternal);
            dxgiDebug!.Dispose();
        }
#endif
    }

    protected void WaitForGpu()
    {
        // Schedule a Signal command in the GPU queue.
        ulong fenceValue = _fenceValues[BackBufferIndex];
        DirectQueue.Signal(_frameFence, fenceValue);

        // Wait until the Signal has been processed.
        if (_frameFence.SetEventOnCompletion(fenceValue, _frameFenceEvent).Success)
        {
            _frameFenceEvent.WaitOne();

            // Increment the fence value for the current frame.
            _fenceValues[BackBufferIndex]++;
        }
    }

    private void HandleDeviceLost()
    {

    }

    private void CreateWindowSizeDependentResources()
    {
        // Wait until all previous GPU work is complete.
        WaitForGpu();

        // Release resources that are tied to the swap chain and update fence values.
        for (int i = 0; i < MainWindow.BackBufferCount; i++)
        {
            if (_renderTargets[i] is not null)
            {
                _renderTargets[i].Dispose();
                RenderTargetViewHeap.ReleaseDescriptor(_renderTargetDescriptorIndexes[i]);
            }

            _fenceValues[i] = _fenceValues[BackBufferIndex];
        }

        DepthStencilTexture?.Dispose();
        if (_depthStencilViewDescriptorIndex != ~0u)
        {
            DepthStencilViewHeap.ReleaseDescriptor(_depthStencilViewDescriptorIndex);
        }

        if (SwapChain is null)
        {
            // Create SwapChain
            using (IDXGISwapChain1 tempSwapChain = MainWindow.CreateSwapChain(_dxgiFactory, DirectQueue, ColorFormat))
            {
                SwapChain = tempSwapChain.QueryInterface<IDXGISwapChain3>();
            }
        }
        else
        {
            SizeF size = MainWindow.ClientSize;
            Format backBufferFormat = SwapChain.Description1.Format;

            // If the swap chain already exists, resize it.
            uint backBufferCount = MainWindow.BackBufferCount;
            Result hr = SwapChain.ResizeBuffers(
                backBufferCount,
                (uint)size.Width,
                (uint)size.Height,
                backBufferFormat,
                _isTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
                );

            if (hr == DXGI.ResultCode.DeviceRemoved || hr == DXGI.ResultCode.DeviceReset)
            {
#if DEBUG
                Result logResult = (hr == DXGI.ResultCode.DeviceRemoved) ? Device.DeviceRemovedReason : hr;
                Debug.WriteLine($"Device Lost on ResizeBuffers: Reason code {logResult}");
#endif
                // If the device was removed for any reason, a new device and swap chain will need to be created.
                HandleDeviceLost();

                // Everything is set up now. Do not continue execution of this method. HandleDeviceLost will reenter this method
                // and correctly set up the new device.
                return;
            }
            else
            {
                hr.CheckError();
            }
        }

        // Handle color space settings for HDR
        UpdateColorSpace();

        // Create a RTV for each frame.
        for (uint i = 0; i < _renderTargets.Length; i++)
        {
            _renderTargets[i] = SwapChain.GetBuffer<ID3D12Resource>(i);
            _renderTargetDescriptorIndexes[i] = RenderTargetViewHeap.AllocateDescriptor();

            CpuDescriptorHandle rtvHandle = RenderTargetViewHeap.GetCpuHandle(_renderTargetDescriptorIndexes[i]);
            Device.CreateRenderTargetView(_renderTargets[i], null, rtvHandle);
        }

        BackBufferIndex = SwapChain.CurrentBackBufferIndex;

        if (_depthStencilFormat != Format.Unknown)
        {
            ResourceDescription depthStencilDesc = ResourceDescription.Texture2D(_depthStencilFormat, (uint)MainWindow.ClientSize.Width, (uint)MainWindow.ClientSize.Height, 1, 1);
            depthStencilDesc.Flags |= ResourceFlags.AllowDepthStencil;

            DepthStencilTexture = Device.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                depthStencilDesc,
                ResourceStates.DepthWrite,
                new(_depthStencilFormat, 1.0f, 0)
                );
            DepthStencilTexture.Name = "DepthStencil Texture";

            DepthStencilViewDescription dsViewDesc = new()
            {
                Format = _depthStencilFormat,
                ViewDimension = DepthStencilViewDimension.Texture2D
            };

            _depthStencilViewDescriptorIndex = DepthStencilViewHeap.AllocateDescriptor();
            CpuDescriptorHandle dsvHandle = DepthStencilViewHeap.GetCpuHandle(_depthStencilViewDescriptorIndex);
            Device.CreateDepthStencilView(DepthStencilTexture, dsViewDesc, dsvHandle);
        }

    }

    private void ResizeSwapchain()
    {
        CreateWindowSizeDependentResources();
    }

    protected override void Draw(AppTime time)
    {
        CommandAllocator.Reset();
        CommandList.Reset(CommandAllocator);

        ReadOnlySpan<ID3D12DescriptorHeap> heaps =
        [
            ShaderResourceViewHeap.ShaderVisibleHeap!,
            SamplerHeap.ShaderVisibleHeap!
        ];
        CommandList.SetDescriptorHeaps(2, heaps);

        CommandList.BeginEvent("Frame");

        // Indicate that the back buffer will be used as a render target.
        CommandList.ResourceBarrierTransition(_renderTargets[BackBufferIndex], ResourceStates.Present, ResourceStates.RenderTarget);

        if (!UseRenderPass)
        {
            CommandList.OMSetRenderTargets(ColorTextureView, DepthStencilView);
        }

        CommandList.RSSetViewport(Viewport);
        CommandList.RSSetScissorRect((int)MainWindow.ClientSize.Width, (int)MainWindow.ClientSize.Height);

        OnRender();

        if (UseRenderPass)
        {
            CommandList.EndRenderPass();
        }

        base.Draw(time);

        // Indicate that the back buffer will now be used to present.
        CommandList.ResourceBarrierTransition(_renderTargets[BackBufferIndex], ResourceStates.RenderTarget, ResourceStates.Present);
        CommandList.EndEvent();
        CommandList.Close();

        // Execute the command list.
        DirectQueue.ExecuteCommandList(CommandList);
    }

    protected abstract void OnRender();

    protected override bool BeginDraw()
    {
        // Check for window size changes and resize the swapchain if needed.
        SwapChainDescription1 swapChainDesc = SwapChain.Description1;

        if (MainWindow.ClientSize.Width != swapChainDesc.Width ||
            MainWindow.ClientSize.Height != swapChainDesc.Height)
        {
            ResizeSwapchain();
        }

        return true;
    }

    protected override void EndDraw()
    {
        uint syncInterval = 1;
        PresentFlags presentFlags = PresentFlags.None;
        if (!EnableVerticalSync)
        {
            syncInterval = 0;
            if (_isTearingSupported)
            {
                presentFlags = PresentFlags.AllowTearing;
            }
        }

        Result result = SwapChain.Present(syncInterval, presentFlags);

        // If the device was reset we must completely reinitialize the renderer.
        if (result == DXGI.ResultCode.DeviceRemoved || result == DXGI.ResultCode.DeviceReset)
        {
#if DEBUG
            Result logResult = (result == DXGI.ResultCode.DeviceRemoved) ? Device.DeviceRemovedReason : result;
            Debug.WriteLine($"Device Lost on Present: Reason code {logResult}");
#endif
            HandleDeviceLost();
        }
        else
        {
            result.CheckError();

            MoveToNextFrame();

            if (!_dxgiFactory.IsCurrent)
            {
                UpdateColorSpace();
            }
        }
    }

    private void MoveToNextFrame()
    {
        // Schedule a Signal command in the queue.
        ulong currentFenceValue = _fenceValues[BackBufferIndex];
        DirectQueue.Signal(_frameFence, currentFenceValue);

        // Update the back buffer index.
        BackBufferIndex = SwapChain.CurrentBackBufferIndex;

        // If the next frame is not ready to be rendered yet, wait until it is ready.
        if (_frameFence.CompletedValue < _fenceValues[BackBufferIndex])
        {
            _frameFence.SetEventOnCompletion(_fenceValues[BackBufferIndex], _frameFenceEvent).CheckError();
            _frameFenceEvent.WaitOne();
        }

        // Set the fence value for the next frame.
        _fenceValues[BackBufferIndex] = currentFenceValue + 1;
    }

    private void UpdateColorSpace()
    {
        if (!_dxgiFactory.IsCurrent)
        {
            // Output information is cached on the DXGI Factory. If it is stale we need to create a new factory.
            _dxgiFactory.Dispose();
            _dxgiFactory = CreateDXGIFactory2<IDXGIFactory4>(_dxgiDebug);
        }

        ColorSpace = ColorSpaceType.RgbFullG22NoneP709;
        if (SwapChain is null)
            return;

        bool isDisplayHDR10 = false;

        // To detect HDR support, we will need to check the color space in the primary
        // DXGI output associated with the app at this point in time
        // (using window/display intersection).

        // Get the retangle bounds of the app window.
        Rectangle windowBounds = MainWindow.Bounds;
        if (windowBounds.IsEmpty)
            return;

        IDXGIOutput? bestOutput = default;
        int bestIntersectArea = -1;

        for (uint adapterIndex = 0;
            _dxgiFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter).Success;
            adapterIndex++)
        {
            for (uint outputIndex = 0;
                adapter.EnumOutputs(outputIndex, out IDXGIOutput? output).Success;
                outputIndex++)
            {
                // Get the rectangle bounds of current output.
                OutputDescription outputDesc = output.Description;
                RawRect r = outputDesc.DesktopCoordinates;

                // Compute the intersection
                int intersectArea = ComputeIntersectionArea(in windowBounds, in r);
                if (intersectArea > bestIntersectArea)
                {
                    bestOutput = output;
                    bestIntersectArea = intersectArea;
                }
                else
                {
                    output?.Dispose();
                }
            }

            adapter.Dispose();
        }

        if (bestOutput is not null)
        {
            using IDXGIOutput6? output6 = bestOutput.QueryInterfaceOrNull<IDXGIOutput6>();
            if (output6 != null)
            {
                OutputDescription1 outputDesc = output6.Description1;

                if (outputDesc.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020)
                {
                    // Display output is HDR10.
                    isDisplayHDR10 = true;
                }
            }

            bestOutput.Dispose();
        }

        if (isDisplayHDR10)
        {
            switch (ColorFormat)
            {
                case Format.R10G10B10A2_UNorm:
                    // The application creates the HDR10 signal.
                    ColorSpace = ColorSpaceType.RgbFullG2084NoneP2020;
                    break;

                case Format.R16G16B16A16_Float:
                    // The system creates the HDR10 signal; application uses linear values.
                    ColorSpace = ColorSpaceType.RgbFullG10NoneP709;
                    break;

                default:
                    ColorSpace = ColorSpaceType.RgbFullG22NoneP709;
                    break;
            }
        }

        SwapChainColorSpaceSupportFlags colorSpaceSupport = SwapChain.CheckColorSpaceSupport(ColorSpace);
        if ((colorSpaceSupport & SwapChainColorSpaceSupportFlags.Present) != SwapChainColorSpaceSupportFlags.None)
        {
            SwapChain.SetColorSpace1(ColorSpace);
        }
    }

    protected static ReadOnlyMemory<byte> CompileBytecode(
        DxcShaderStage stage,
        string shaderName,
        string entryPoint,
        DxcShaderModel? shaderModel = default)
    {
        string assetsPath = Path.Combine(System.AppContext.BaseDirectory, "Shaders");
        string fileName = Path.Combine(assetsPath, shaderName);
        string shaderSource = File.ReadAllText(fileName);

        DxcCompilerOptions options = new();
        if (shaderModel != null)
        {
            options.ShaderModel = shaderModel.Value;
        }
        else
        {
            options.ShaderModel = DxcShaderModel.Model6_4;
        }

        using (ShaderIncludeHandler includeHandler = new(assetsPath))
        {
            using IDxcResult results = DxcCompiler.Compile(stage, shaderSource, entryPoint, options,
                fileName: fileName,
                includeHandler: includeHandler);
            if (results.GetStatus().Failure)
            {
                throw new Exception(results.GetErrors());
            }

            return results.GetObjectBytecodeMemory();
        }
    }

    private static int ComputeIntersectionArea(in Rectangle rect1, in RawRect rect2)
    {
        return Math.Max(0, Math.Min(rect1.Right, rect2.Right) - Math.Max(rect1.Left, rect2.Left)) * Math.Max(0, Math.Min(rect1.Bottom, rect2.Bottom) - Math.Max(rect1.Top, rect2.Top));
    }

    private class ShaderIncludeHandler : CallbackBase, IDxcIncludeHandler
    {
        private readonly string[] _includeDirectories;
        private readonly Dictionary<string, SourceCodeBlob> _sourceFiles = new();

        public ShaderIncludeHandler(params string[] includeDirectories)
        {
            _includeDirectories = includeDirectories;
        }

        protected override void DisposeCore(bool disposing)
        {
            foreach (var pinnedObject in _sourceFiles.Values)
                pinnedObject?.Dispose();

            _sourceFiles.Clear();
        }

        public Result LoadSource(string fileName, out IDxcBlob? includeSource)
        {
            if (fileName.StartsWith("./"))
                fileName = fileName.Substring(2);

            var includeFile = GetFilePath(fileName);

            if (string.IsNullOrEmpty(includeFile))
            {
                includeSource = default;

                return Result.Fail;
            }

            if (!_sourceFiles.TryGetValue(includeFile, out SourceCodeBlob? sourceCodeBlob))
            {
                byte[] data = NewMethod(includeFile);

                sourceCodeBlob = new SourceCodeBlob(data);
                _sourceFiles.Add(includeFile, sourceCodeBlob);
            }

            includeSource = sourceCodeBlob.Blob;

            return Result.Ok;
        }

        private static byte[] NewMethod(string includeFile) => File.ReadAllBytes(includeFile);

        private string? GetFilePath(string fileName)
        {
            for (int i = 0; i < _includeDirectories.Length; i++)
            {
                var filePath = Path.GetFullPath(Path.Combine(_includeDirectories[i], fileName));

                if (File.Exists(filePath))
                    return filePath;
            }

            return null;
        }


        private class SourceCodeBlob : IDisposable
        {
            private byte[] _data;
            private GCHandle _dataPointer;
            private IDxcBlobEncoding? _blob;

            internal IDxcBlob? Blob { get => _blob; }

            public SourceCodeBlob(byte[] data)
            {
                _data = data;

                _dataPointer = GCHandle.Alloc(data, GCHandleType.Pinned);

                _blob = DxcCompiler.Utils.CreateBlob(_dataPointer.AddrOfPinnedObject(), (uint)data.Length, Vortice.Dxc.Dxc.DXC_CP_UTF8);
            }

            public void Dispose()
            {
                //_blob?.Dispose();
                _blob = null;

                if (_dataPointer.IsAllocated)
                    _dataPointer.Free();
                _dataPointer = default;
            }
        }
    }
}
