// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

namespace Text;

internal class DrawTextApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;
    private ID3D11Texture2D _texture;
    private ID3D11ShaderResourceView _textureSRV;
    private ID3D11RenderTargetView _textureRTV;
    private ID3D11SamplerState _textureSampler;

    // text related objects
    static IDWriteFactory _directWriteFactory;
    static IDWriteTextFormat _textFormat;
    static ID2D1Factory7 _direct2dFactory;
    static ID2D1SolidColorBrush _brush;
    static ID2D1RenderTarget _renderTarget2d;

    protected override void Initialize()
    {
        ReadOnlySpan<VertexPositionTexture> source =
          [
              new VertexPositionTexture(new Vector3(-0.5f, 0.5f, 0.0f), new Vector2(0, 0)),
              new VertexPositionTexture(new Vector3(0.5f, 0.5f, 0.0f), new Vector2(1, 0)),
              new VertexPositionTexture(new Vector3(0.5f, -0.5f, 0.0f), new Vector2(1, 1)),
              new VertexPositionTexture(new Vector3(-0.5f, -0.5f, 0.0f), new Vector2(0, 1))
          ];

        _vertexBuffer = Device.CreateBuffer(source, BindFlags.VertexBuffer);

        ReadOnlySpan<ushort> quadIndices = [0, 1, 2, 0, 2, 3];
        _indexBuffer = Device.CreateBuffer(quadIndices, BindFlags.IndexBuffer);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("TextureShaders.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("TextureShaders.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPositionTexture.InputElements, vertexShaderByteCode.Span);

        // create texture and associated resources
        Texture2DDescription desc = new()
        {
            ArraySize = 1,
            CPUAccessFlags = CpuAccessFlags.None,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Format = Format.B8G8R8A8_UNorm,
            Height = 378,
            MipLevels = 1,
            MiscFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1,0),
            Usage = ResourceUsage.Default,
            Width = 720
        };
        _texture = Device.CreateTexture2D(desc);
        _textureSRV = Device.CreateShaderResourceView(_texture);
        _textureSampler = Device.CreateSamplerState(SamplerDescription.LinearWrap);
        _textureRTV = Device.CreateRenderTargetView(_texture);
        DeviceContext.ClearRenderTargetView(_textureRTV, Colors.MediumBlue);

        // create DWrite factory - used to create some of the objects we need.
        _directWriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();

        // create an instance of IDWriteTextFormat - this describes the text's appearance.
        _textFormat = _directWriteFactory.CreateTextFormat(
            "Arial",
            FontWeight.Bold,
            FontStyle.Normal,
            FontStretch.Normal,
            100);

        // set text alignment
        _textFormat.TextAlignment = TextAlignment.Center;
        _textFormat.ParagraphAlignment = ParagraphAlignment.Center;

        // create Direct2D factory
        _direct2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory7>(Vortice.Direct2D1.FactoryType.SingleThreaded, DebugLevel.Information);

        // draw text onto the texture
        DrawText("Hello Text!", _texture);
    }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            _vertexShader.Dispose();
            _pixelShader.Dispose();
            _inputLayout.Dispose();
            _textureSRV.Dispose();
            _textureRTV.Dispose();
            _texture.Dispose();
            _textureSampler.Dispose();
            _textFormat.Dispose();
            _brush?.Dispose();
            _direct2dFactory.Dispose();
            _directWriteFactory.Dispose();
        }

        base.Dispose(dispose);
    }

    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
        // input assembler
        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionTexture.SizeInBytes);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
        // vertex shader
        DeviceContext.VSSetShader(_vertexShader);
        // pixel shader
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.PSSetShaderResource(0, _textureSRV);
        DeviceContext.PSSetSampler(0, _textureSampler);
        // draw
        DeviceContext.DrawIndexed(6, 0, 0);
    }

    private void DrawText(string text, ID3D11Texture2D target)
    {
        // the dxgi runtime layer provides the video memory sharing mechanism to allow
        // Direct2D and Direct3D to work together. One way to use the two technologies
        // together is by obtaining IDXGISurface and then use CreateDxgiSurfaceRenderTarget
        // to create an ID2D1RenderTarget, which can then be drawn to with Direct2D.

        //IDXGISurface1 dxgiSurface = ID3D11Texture2D.QueryInterface<IDXGISurface1>(target); not supported
        IDXGISurface1 dxgiSurface = target.QueryInterface<IDXGISurface1>();

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

        // Create the brush
        _brush?.Release();
        _brush = _renderTarget2d.CreateSolidColorBrush(Colors.Black);

        Rect layoutRect = new (0, 0, 720, 378);

        _renderTarget2d.BeginDraw();
        _renderTarget2d.Transform = Matrix3x2.Identity;
        _renderTarget2d.Clear(Colors.White);
        _renderTarget2d.DrawText(text, _textFormat, layoutRect, _brush);
        _renderTarget2d.EndDraw();

        dxgiSurface.Dispose();
        _renderTarget2d.Dispose();
    }

    public static void Main()
    {
        using DrawTextApp app = new();
        app.Run();
    }
}
