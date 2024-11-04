// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpGen.Runtime;
using Vortice.DXGI;
using WinRT;

namespace Vortice.Framework;

public partial class WinUIAppWindow : AppWindow
{
    private Vortice.WinUI.ISwapChainPanelNative? _swapChainPanelNative;

    public WinUIAppWindow(Window window, SwapChainPanel panel)
    {
        Window = window;
        AppWindow = Window.AppWindow; // WindowHelper.GetAppWindow(Window);
        Panel = panel;
        Panel.SizeChanged += OnSwapChainPanelSizeChanged;
        Panel.CompositionScaleChanged += OnSwapChainPanelCompositionScaleChanged;
    }

    public Window Window { get; }
    public Microsoft.UI.Windowing.AppWindow AppWindow { get; }

    public SwapChainPanel Panel { get; }

    /// <inheritdoc />
    public override string Title
    {
        get
        {
            return AppWindow.Title;
        }
        set
        {
            AppWindow.Title = value;
        }
    }

    /// <inheritdoc />
    public override SizeF ClientSize
    {
        get
        {
            return new SizeF(
                MathF.Max(1.0f, (float)Panel.ActualWidth * Panel.CompositionScaleX + 0.5f),
                MathF.Max(1.0f, (float)Panel.ActualHeight * Panel.CompositionScaleY + 0.5f));
        }
    }

    /// <inheritdoc />
    public override Rectangle Bounds
    {
        get
        {
            return new Rectangle(
                AppWindow.Position.X,
                AppWindow.Position.Y,
                AppWindow.Size.Width,
                AppWindow.Size.Height);
        }
    }

    public void OnShutdown()
    {
        Panel.SizeChanged -= OnSwapChainPanelSizeChanged;
        Panel.CompositionScaleChanged -= OnSwapChainPanelCompositionScaleChanged;

        _swapChainPanelNative.SetSwapChain(null);
        _swapChainPanelNative.Dispose();
    }

    public override IDXGISwapChain1 CreateSwapChain(IDXGIFactory2 factory, ComObject deviceOrCommandQueue, Format colorFormat)
    {
        SizeF size = ClientSize;
        Format backBufferFormat = Utilities.ToSwapChainFormat(colorFormat);

        bool isTearingSupported = false;
        using (IDXGIFactory5? factory5 = factory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                isTearingSupported = factory5.PresentAllowTearing;
            }
        }

        SwapChainDescription1 desc = new()
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
            Format = backBufferFormat,
            BufferCount = BackBufferCount,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Ignore,
            Flags = isTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        using (IDXGISwapChain1 tempSwapChain = factory.CreateSwapChainForComposition(deviceOrCommandQueue, desc, null))
        {
            IDXGISwapChain3 swapChain = tempSwapChain.QueryInterface<IDXGISwapChain3>();

            Guid guid = typeof(Vortice.WinUI.ISwapChainPanelNative).GUID;
            Result result = ((IWinRTObject)Panel).NativeObject.TryAs(guid, out nint swapChainPanelNativeHandle);
            result.CheckError();

            _swapChainPanelNative = new Vortice.WinUI.ISwapChainPanelNative(swapChainPanelNativeHandle);
            _swapChainPanelNative.SetSwapChain(swapChain).CheckError();

            swapChain.MatrixTransform = new Matrix3x2
            {
                M11 = 1.0f / Panel.CompositionScaleX,
                M22 = 1.0f / Panel.CompositionScaleY
            };
            return swapChain;
        }
    }

    private void OnSwapChainPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        OnSizeChanged();
    }

    private void OnSwapChainPanelCompositionScaleChanged(SwapChainPanel sender, object e)
    {
        OnSizeChanged();
    }
}
