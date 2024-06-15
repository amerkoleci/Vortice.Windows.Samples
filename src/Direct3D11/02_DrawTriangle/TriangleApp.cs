// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

public class TriangleApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;

    protected override void Initialize()
    {
        ReadOnlySpan<VertexPositionColor> triangleVertices = stackalloc VertexPositionColor[]
        {
            new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
        };
        _vertexBuffer = Device.CreateBuffer(triangleVertices, BindFlags.VertexBuffer);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);

        _inputLayout = Device.CreateInputLayout(VertexPositionColor.InputElements, vertexShaderByteCode.Span);
    }

    protected override void OnShutdown()
    {
        _vertexBuffer.Dispose();
        _vertexShader.Dispose();
        _pixelShader.Dispose();
        _inputLayout.Dispose();

        base.OnShutdown();
    }

    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionColor.SizeInBytes);
        DeviceContext.Draw(3, 0);
    }

    public static void Main()
    {
        TriangleApp app = new();
        app.Run();
    }
}
