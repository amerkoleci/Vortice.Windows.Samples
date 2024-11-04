// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;

class HelloWindowApp : D3D11Application
{
    private ID2D1Factory1? _direct2dFactory;
    private ID2D1RenderTarget _renderTarget2d;
    private ID2D1SolidColorBrush _brush;

    protected override void Initialize()
    {
        base.Initialize();

        // Create Direct2D factory
        _direct2dFactory = D2D1CreateFactory<ID2D1Factory1>(FactoryType.SingleThreaded, DebugLevel.Information);

        using IDXGISurface1 dxgiSurface = ColorTexture.QueryInterface<IDXGISurface1>();

        RenderTargetProperties rtvProps = new()
        {
            DpiX = 0,
            DpiY = 0,
            MinLevel = Vortice.Direct2D1.FeatureLevel.Default,
            PixelFormat = Vortice.DCommon.PixelFormat.Premultiplied,
            Type = RenderTargetType.Hardware,
            Usage = RenderTargetUsage.None
        };
        _renderTarget2d = _direct2dFactory.CreateDxgiSurfaceRenderTarget(dxgiSurface, rtvProps);

        _brush = _renderTarget2d.CreateSolidColorBrush(Colors.Black);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _brush!.Dispose();
        _renderTarget2d!.Dispose();
        _direct2dFactory!.Dispose();
    }

    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        Vector2 screenCenter = new Vector2(MainWindow.ClientSize.Width / 2f, MainWindow.ClientSize.Height / 2f);

        _renderTarget2d.BeginDraw();
        _renderTarget2d.Transform = Matrix3x2.Identity;
        _renderTarget2d.Clear(Colors.White);
        _renderTarget2d.DrawRectangle(new Rect(screenCenter.X, screenCenter.Y, 256, 256), _brush);
        //_renderTarget2d.DrawText(text, _textFormat, layoutRect, _brush);
        _renderTarget2d.EndDraw();

        //_renderTarget2d.Dispose();
    }

    public static void Main()
    {
        HelloWindowApp app = new();
        app.Run();
    }
}
