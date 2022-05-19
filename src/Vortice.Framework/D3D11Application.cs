// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
using static Vortice.Framework.Utilities;

namespace Vortice.Framework;

/// <summary>
/// Class that handles all logic to have D3D11 running application.
/// </summary>
public abstract class D3D11Application : Application
{
    private static readonly FeatureLevel[] s_featureLevels = new[]
    {
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    };

    private readonly Format _colorFormat;
    private readonly Format _depthStencilFormat;
    private readonly int _backBufferCount;
    private IDXGIFactory2 _dxgiFactory;
    private readonly bool _isTearingSupported;
    private readonly FeatureLevel _featureLevel;

    protected D3D11Application(
        Format colorFormat = Format.B8G8R8A8_UNorm,
        Format depthStencilFormat = Format.D32_Float,
        int backBufferCount = 2)
    {
        _colorFormat = colorFormat;
        _depthStencilFormat = depthStencilFormat;
        _backBufferCount = backBufferCount;

        _dxgiFactory = CreateDXGIFactory1<IDXGIFactory2>();

        using (IDXGIFactory5? factory5 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                _isTearingSupported = factory5.PresentAllowTearing;
            }
        }

        using (IDXGIAdapter1 adapter = GetHardwareAdapter())
        {
            DeviceCreationFlags creationFlags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            if (SdkLayersAvailable())
            {
                creationFlags |= DeviceCreationFlags.Debug;
            }
#endif

            if (D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                creationFlags,
                s_featureLevels,
                out ID3D11Device tempDevice, out _featureLevel, out ID3D11DeviceContext tempContext).Failure)
            {
                // If the initialization fails, fall back to the WARP device.
                // For more information on WARP, see:
                // http://go.microsoft.com/fwlink/?LinkId=286690
                D3D11CreateDevice(
                    IntPtr.Zero,
                    DriverType.Warp,
                    creationFlags,
                    s_featureLevels,
                    out tempDevice, out _featureLevel, out tempContext).CheckError();
            }

            Device = tempDevice.QueryInterface<ID3D11Device1>();
            DeviceContext = tempContext.QueryInterface<ID3D11DeviceContext1>();
            tempContext.Dispose();
            tempDevice.Dispose();
        }

        IntPtr hwnd = MainWindow.Handle;

        int backBufferWidth = Math.Max(MainWindow.ClientSize.Width, 1);
        int backBufferHeight = Math.Max(MainWindow.ClientSize.Height, 1);
        Format backBufferFormat = ToSwapChainFormat(colorFormat);

        SwapChainDescription1 swapChainDescription = new()
        {
            Width = backBufferWidth,
            Height = backBufferHeight,
            Format = backBufferFormat,
            BufferCount = _backBufferCount,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = _isTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        SwapChainFullscreenDescription fullscreenDescription = new SwapChainFullscreenDescription
        {
            Windowed = true
        };

        SwapChain = _dxgiFactory.CreateSwapChainForHwnd(Device, hwnd, swapChainDescription, fullscreenDescription);
        _dxgiFactory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);

        ColorTexture = SwapChain.GetBuffer<ID3D11Texture2D>(0);
        RenderTargetViewDescription renderTargetViewDesc = new(RenderTargetViewDimension.Texture2D, colorFormat);
        ColorTextureView = Device.CreateRenderTargetView(ColorTexture, renderTargetViewDesc);

        // Create depth stencil texture if required
        if (depthStencilFormat != Format.Unknown)
        {
            DepthStencilTexture = Device.CreateTexture2D(depthStencilFormat, backBufferWidth, backBufferHeight, 1, 1, null, BindFlags.DepthStencil);
            DepthStencilView = Device.CreateDepthStencilView(DepthStencilTexture!, new DepthStencilViewDescription(DepthStencilTexture, DepthStencilViewDimension.Texture2D));
        }
    }

    public ID3D11Device1 Device { get; }
    public ID3D11DeviceContext1 DeviceContext { get; }
    public FeatureLevel FeatureLevel => _featureLevel;
    public IDXGISwapChain1 SwapChain { get; }

    public Format ColorFormat => _colorFormat;
    public ID3D11Texture2D ColorTexture { get; private set; }
    public ID3D11RenderTargetView ColorTextureView { get; private set; }

    public Format DepthStencilFormat => _depthStencilFormat;
    public ID3D11Texture2D? DepthStencilTexture { get; private set; }
    public ID3D11DepthStencilView? DepthStencilView { get; private set; }

    /// <summary>
    /// Gets the viewport.
    /// </summary>
    public Viewport Viewport => new Viewport(MainWindow.ClientSize.Width, MainWindow.ClientSize.Height);

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            ColorTexture.Dispose();
            ColorTextureView.Dispose();
            DepthStencilTexture?.Dispose();
            DepthStencilView?.Dispose();

            SwapChain.Dispose();
            DeviceContext.Dispose();
            Device.Dispose();
            _dxgiFactory.Dispose();

#if DEBUG
            if (DXGIGetDebugInterface1(out IDXGIDebug1? dxgiDebug).Success)
            {
                dxgiDebug!.ReportLiveObjects(DebugAll, ReportLiveObjectFlags.Summary | ReportLiveObjectFlags.IgnoreInternal);
                dxgiDebug!.Dispose();
            }
#endif
        }

        base.Dispose(dispose);
    }

    private IDXGIAdapter1 GetHardwareAdapter()
    {
        IDXGIFactory6? factory6 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory6>();
        if (factory6 != null)
        {
            for (int adapterIndex = 0;
                factory6.EnumAdapterByGpuPreference(adapterIndex, GpuPreference.HighPerformance, out IDXGIAdapter1? adapter).Success;
                adapterIndex++)
            {
                if (adapter == null)
                {
                    continue;
                }

                AdapterDescription1 desc = adapter.Description1;

                if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                {
                    // Don't select the Basic Render Driver adapter.
                    adapter.Dispose();
                    continue;
                }

                factory6.Dispose();
                return adapter;
            }

            factory6.Dispose();
        }

        for (int adapterIndex = 0;
            _dxgiFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter).Success;
            adapterIndex++)
        {
            AdapterDescription1 desc = adapter.Description1;

            if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
            {
                // Don't select the Basic Render Driver adapter.
                adapter.Dispose();
                continue;
            }

            return adapter;
        }

        throw new InvalidOperationException("Cannot detect D3D11 adapter");
    }

    private void HandleDeviceLost()
    {

    }

    private void UpdateColorSpace()
    {
        if (!_dxgiFactory.IsCurrent)
        {
            // Output information is cached on the DXGI Factory. If it is stale we need to create a new factory.
            _dxgiFactory.Dispose();
            _dxgiFactory = CreateDXGIFactory1<IDXGIFactory2>();
        }
    }

    private void ResizeSwapchain()
    {
        // Clear the previous window size specific context.
        DeviceContext.UnsetRenderTargets();
        ColorTextureView.Dispose();
        DepthStencilView?.Dispose();
        ColorTexture.Dispose();
        DepthStencilTexture?.Dispose();
        DeviceContext.Flush();

        int backBufferWidth = Math.Max(MainWindow.ClientSize.Width, 1);
        int backBufferHeight = Math.Max(MainWindow.ClientSize.Height, 1);
        Format backBufferFormat = ToSwapChainFormat(_colorFormat);

        // If the swap chain already exists, resize it.
        Result hr = SwapChain.ResizeBuffers(
            _backBufferCount,
            backBufferWidth,
            backBufferHeight,
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

        ColorTexture = SwapChain.GetBuffer<ID3D11Texture2D>(0);
        RenderTargetViewDescription renderTargetViewDesc = new(RenderTargetViewDimension.Texture2D, _colorFormat);
        ColorTextureView = Device.CreateRenderTargetView(ColorTexture, renderTargetViewDesc);

        // Create depth stencil texture if required
        if (_depthStencilFormat != Format.Unknown)
        {
            DepthStencilTexture = Device.CreateTexture2D(_depthStencilFormat, backBufferWidth, backBufferHeight, 1, 1, null, BindFlags.DepthStencil);
            DepthStencilView = Device.CreateDepthStencilView(DepthStencilTexture!, new DepthStencilViewDescription(DepthStencilTexture, DepthStencilViewDimension.Texture2D));
        }
    }

    protected internal override void Render()
    {
        DeviceContext.OMSetRenderTargets(ColorTextureView, DepthStencilView);
        DeviceContext.RSSetViewport(Viewport);
        DeviceContext.RSSetScissorRect(0, 0, MainWindow.ClientSize.Width, MainWindow.ClientSize.Height);

        OnRender();
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
        int syncInterval = 1;
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

        // Discard the contents of the render target.
        // This is a valid operation only when the existing contents will be entirely
        // overwritten. If dirty or scroll rects are used, this call should be removed.
        DeviceContext.DiscardView(ColorTextureView);

        if (DepthStencilView != null)
        {
            // Discard the contents of the depth stencil.
            DeviceContext.DiscardView(DepthStencilView);
        }

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

            if (!_dxgiFactory.IsCurrent)
            {
                UpdateColorSpace();
            }
        }
    }
}
