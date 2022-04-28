// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;
using static Vortice.Framework.Utilities;
using Vortice.Direct3D12.Debug;
using System.Diagnostics;
using Vortice.DXGI.Debug;

namespace Vortice.Framework;

/// <summary>
/// Class that handles all logic to have D3D12 running application.
/// </summary>
public abstract class D3D12Application : Application
{
    private readonly Format _colorFormat;
    private readonly Format _depthStencilFormat;
    private readonly int _backBufferCount;
    private readonly IDXGIFactory4 _dxgiFactory;
    private readonly bool _isTearingSupported;

    protected D3D12Application(Format colorFormat = Format.B8G8R8A8_UNorm,
        Format depthStencilFormat = Format.D32_Float,
        int backBufferCount = 2)
    {
        _colorFormat = colorFormat;
        _depthStencilFormat = depthStencilFormat;
        _backBufferCount = backBufferCount;

        bool dxgiDebug = false;
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
                dxgiDebug = true;

                dxgiInfoQueue!.SetBreakOnSeverity(DebugAll, InfoQueueMessageSeverity.Error, true);
                dxgiInfoQueue!.SetBreakOnSeverity(DebugAll, InfoQueueMessageSeverity.Corruption, true);

                int[] hide = new[]
                {
                    80 /* IDXGISwapChain::GetContainingOutput: The swapchain's adapter does not control the output on which the swapchain's window resides. */,
                };
                DXGI.Debug.InfoQueueFilter filter = new ();
                filter.DenyList = new DXGI.Debug.InfoQueueFilterDescription
                {
                    Ids = hide
                };
                dxgiInfoQueue!.AddStorageFilterEntries(DebugDxgi, filter);
                dxgiInfoQueue.Dispose();

            }
        }
#endif

        _dxgiFactory = CreateDXGIFactory2<IDXGIFactory4>(dxgiDebug);

        using (IDXGIFactory5? factory5 = _dxgiFactory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                _isTearingSupported = factory5.PresentAllowTearing;
            }
        }
    }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            //ColorTexture.Dispose();
            //ColorTextureView.Dispose();
            //DepthStencilTexture?.Dispose();
            //DepthStencilView?.Dispose();
            //
            //SwapChain.Dispose();
            //DeviceContext.Dispose();
            //Device.Dispose();
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

    protected internal override void Render()
    {
        //DeviceContext.RSSetViewport(Viewport);
        //DeviceContext.RSSetScissorRect(0, 0, MainWindow.ClientSize.Width, MainWindow.ClientSize.Height);
        //DeviceContext.OMSetRenderTargets(ColorTextureView, DepthStencilView);

        OnRender();
    }

    protected abstract void OnRender();
}
