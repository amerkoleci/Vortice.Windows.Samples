// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

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

    private readonly IDXGIFactory2 _dxgiFactory;
    private readonly FeatureLevel _featureLevel;

    protected D3D11Application()
    {
        _dxgiFactory = CreateDXGIFactory1<IDXGIFactory2>();

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

        SwapChainDescription1 swapChainDescription = new()
        {
            Width = MainWindow.ClientSize.Width,
            Height = MainWindow.ClientSize.Height,
            Format = Format.R8G8B8A8_UNorm,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore
        };

        SwapChainFullscreenDescription fullscreenDescription = new SwapChainFullscreenDescription
        {
            Windowed = true
        };

        SwapChain = _dxgiFactory.CreateSwapChainForHwnd(Device, hwnd, swapChainDescription, fullscreenDescription);
        _dxgiFactory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
    }

    public ID3D11Device1 Device { get; }
    public ID3D11DeviceContext1 DeviceContext { get; }
    public FeatureLevel FeatureLevel => _featureLevel;
    public IDXGISwapChain1 SwapChain { get; }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
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

    protected override void EndDraw()
    {
        base.EndDraw();

        Result result = SwapChain.Present(1, PresentFlags.None);
        if (result.Failure
            && result.Code == Vortice.DXGI.ResultCode.DeviceRemoved.Code)
        {
            return;
        }
    }
}
