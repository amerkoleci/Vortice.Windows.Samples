// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace Vortice.Framework;

/// <summary>
/// Class that handles all logic to have D3D11 running application.
/// </summary>
public abstract class D3D11Application : Application
{
    private static readonly FeatureLevel[] s_featureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    ];

    private readonly Format _colorFormat;
    private readonly Format _depthStencilFormat;
    private IDXGIFactory2 _dxgiFactory;
    private readonly bool _isTearingSupported;
    private readonly FeatureLevel _featureLevel;

    protected D3D11Application(
        AppPlatform? platform = default,
        DeviceCreationFlags creationFlags = DeviceCreationFlags.BgraSupport,
        Format colorFormat = Format.B8G8R8A8_UNorm,
        Format depthStencilFormat = Format.D32_Float)
        : base(platform)
    {
        _colorFormat = colorFormat;
        _depthStencilFormat = depthStencilFormat;

        _dxgiFactory = CreateDXGIFactory1<IDXGIFactory2>();

        using (IDXGIFactory5? factory5 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                _isTearingSupported = factory5.PresentAllowTearing;
            }
        }

        using IDXGIAdapter1 adapter = GetHardwareAdapter();

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

        CreateWindowSizeDependentResources();
    }

    public ID3D11Device1 Device { get; }
    public ID3D11DeviceContext1 DeviceContext { get; }
    public FeatureLevel FeatureLevel => _featureLevel;
    public IDXGISwapChain1 SwapChain { get; private set; }

    public Format ColorFormat => _colorFormat;
    public ColorSpaceType ColorSpace { get; private set; } = ColorSpaceType.RgbFullG22NoneP709;
    public ID3D11Texture2D ColorTexture { get; private set; }
    public ID3D11RenderTargetView ColorTextureView { get; private set; }

    public Format DepthStencilFormat => _depthStencilFormat;
    public ID3D11Texture2D? DepthStencilTexture { get; private set; }
    public ID3D11DepthStencilView? DepthStencilView { get; private set; }

    /// <summary>
    /// Gets the viewport.
    /// </summary>
    public Viewport Viewport => new(MainWindow.ClientSize.Width, MainWindow.ClientSize.Height);

    public bool DiscardViews { get; set; } = true;

    protected virtual void OnDestroy()
    {

    }

    protected sealed override void OnShutdown()
    {
        DeviceContext.Flush();

        OnDestroy();

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

    private IDXGIAdapter1 GetHardwareAdapter()
    {
        IDXGIFactory6? factory6 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory6>();
        if (factory6 != null)
        {
            for (uint adapterIndex = 0;
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

        for (uint adapterIndex = 0;
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

    private void CreateWindowSizeDependentResources()
    {
        // Clear the previous window size specific context.
        DeviceContext.UnsetRenderTargets();
        ColorTextureView?.Dispose();
        DepthStencilView?.Dispose();
        ColorTexture?.Dispose();
        DepthStencilTexture?.Dispose();
        DeviceContext.Flush();

        if (SwapChain is null)
        {
            SwapChain = MainWindow.CreateSwapChain(_dxgiFactory, Device, ColorFormat);
        }
        else
        {
            SizeF size = MainWindow.ClientSize;
            Format backBufferFormat = SwapChain.Description1.Format;

            // If the swap chain already exists, resize it.
            Result hr = SwapChain.ResizeBuffers(
                MainWindow.BackBufferCount,
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

        UpdateColorSpace();

        ColorTexture = SwapChain.GetBuffer<ID3D11Texture2D>(0);
        RenderTargetViewDescription renderTargetViewDesc = new(RenderTargetViewDimension.Texture2D, _colorFormat);
        ColorTextureView = Device.CreateRenderTargetView(ColorTexture, renderTargetViewDesc);

        // Create depth stencil texture if required
        if (_depthStencilFormat != Format.Unknown)
        {
            DepthStencilTexture = Device.CreateTexture2D(_depthStencilFormat, SwapChain.Description1.Width, SwapChain.Description1.Height, 1, 1, null, BindFlags.DepthStencil);
            DepthStencilView = Device.CreateDepthStencilView(DepthStencilTexture!, new DepthStencilViewDescription(DepthStencilTexture, DepthStencilViewDimension.Texture2D));
        }
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

        using IDXGISwapChain3? swapChain3 = SwapChain!.QueryInterfaceOrNull<IDXGISwapChain3>();
        if (swapChain3 is not null)
        {
            SwapChainColorSpaceSupportFlags colorSpaceSupport = swapChain3.CheckColorSpaceSupport(ColorSpace);
            if ((colorSpaceSupport & SwapChainColorSpaceSupportFlags.Present) != SwapChainColorSpaceSupportFlags.None)
            {
                swapChain3.SetColorSpace1(ColorSpace);
            }
        }
    }

    private void ResizeSwapchain()
    {
        CreateWindowSizeDependentResources();
    }

    protected override void Draw(AppTime time)
    {
        DeviceContext.OMSetRenderTargets(ColorTextureView, DepthStencilView);
        DeviceContext.RSSetViewport(Viewport);
        DeviceContext.RSSetScissorRect(0, 0, (int)MainWindow.ClientSize.Width, (int)MainWindow.ClientSize.Height);

        OnRender();

        base.Draw(time);
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

        if (DiscardViews)
        {
            // Discard the contents of the render target.
            // This is a valid operation only when the existing contents will be entirely
            // overwritten. If dirty or scroll rects are used, this call should be removed.
            DeviceContext.DiscardView(ColorTextureView);

            if (DepthStencilView != null)
            {
                // Discard the contents of the depth stencil.
                DeviceContext.DiscardView(DepthStencilView);
            }
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

    protected static ReadOnlyMemory<byte> CompileBytecode(string shaderName, string entryPoint, string profile)
    {
        string assetsPath = Path.Combine(System.AppContext.BaseDirectory, "Shaders");
        string fileName = Path.Combine(assetsPath, shaderName);
        //string shaderSource = File.ReadAllText(Path.Combine(assetsPath, shaderName));

        ShaderFlags shaderFlags = ShaderFlags.EnableStrictness;
#if DEBUG
        shaderFlags |= ShaderFlags.Debug;
        shaderFlags |= ShaderFlags.SkipValidation;
#else
        shaderFlags |= ShaderFlags.OptimizationLevel3;
#endif

        return Compiler.CompileFromFile(fileName, entryPoint, profile, shaderFlags);
    }

    protected unsafe (ID3D11Texture2D, ID3D11ShaderResourceView) LoadTexture(string filePath, int mipLevels = 0)
    {
        bool onGpu = true;

        ID3D11Texture2D texture;
        if (mipLevels == 0)
        {
            if (onGpu)
            {
                Image image = Image.FromFile(filePath)!;

                texture = Device.CreateTexture2D(image.Format, image.Width, image.Height,
                   mipLevels: 0,
                   bindFlags: BindFlags.ShaderResource | BindFlags.RenderTarget,
                   miscFlags: ResourceOptionFlags.GenerateMips);

                fixed (byte* pData = image.Data.Span)
                {
                    DeviceContext.UpdateSubresource(texture, 0, null, (IntPtr)pData, image.RowPitch, 0);
                }
            }
            else
            {
                // Use Skia to generate mips
                Image[] images = Image.FromFileMipMaps(filePath)!;

                mipLevels = images.Length;
                SubresourceData[] subresources = new SubresourceData[mipLevels];
                for (int i = 0; i < mipLevels; i++)
                {
                    FormatHelper.GetSurfaceInfo(images[i].Format, images[i].Width, images[i].Height, out uint rowPitch, out uint slicePitch);

                    fixed (byte* dataPointer = images[i].Data.Span)
                    {
                        subresources[i] = new SubresourceData(dataPointer, images[i].RowPitch, slicePitch);
                    }
                }

                texture = Device.CreateTexture2D(new Texture2DDescription(
                    images[0].Format, images[0].Width, images[0].Height,
                    mipLevels: (uint)mipLevels,
                    bindFlags: BindFlags.ShaderResource
                    ),  subresources);
            }
        }
        else
        {
            Image image = Image.FromFile(filePath)!;

            texture = Device.CreateTexture2D(image.Data.Span, image.Format, image.Width, image.Height, mipLevels: 1);
        }

        ShaderResourceViewDescription srvDesc = new(texture, ShaderResourceViewDimension.Texture2D, texture.Description.Format);
        ID3D11ShaderResourceView srv = Device.CreateShaderResourceView(texture);

        if (mipLevels == 0 && onGpu)
        {
            DeviceContext.GenerateMips(srv);
        }

        return (texture, srv);
    }

    private static int ComputeIntersectionArea(in Rectangle rect1, in RawRect rect2)
    {
        return Math.Max(0, Math.Min(rect1.Right, rect2.Right) - Math.Max(rect1.Left, rect2.Left)) * Math.Max(0, Math.Min(rect1.Bottom, rect2.Bottom) - Math.Max(rect1.Top, rect2.Top));
    }
}
