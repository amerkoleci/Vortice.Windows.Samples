// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

public sealed class BufferOffsetsApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;

    protected override void Initialize()
    {
        ReadOnlySpan<VertexPosition2DColor> quadVertices = stackalloc VertexPosition2DColor[]
        {
            // Triangle
            new VertexPosition2DColor(new Vector2(0.0f, 0.55f), new Color3(1.0f, 0.0f, 0.0f)),
            new VertexPosition2DColor(new Vector2(0.25f, 0.05f), new Color3(0.0f, 1.0f, 0.0f)),
            new VertexPosition2DColor(new Vector2(-0.25f, 0.05f), new Color3(0.0f, 0.0f, 1.0f)),

            // Quad
            new VertexPosition2DColor(new Vector2(-0.25f, -0.05f), new Color3(0.0f, 0.0f, 1.0f)),
            new VertexPosition2DColor(new Vector2(0.25f, -0.05f), new Color3(0.0f, 1.0f, 0.0f)),
            new VertexPosition2DColor(new Vector2(0.25f, -0.55f), new Color3(1.0f, 0.0f, 0.0f)),
            new VertexPosition2DColor(new Vector2(-0.25f, -0.55f), new Color3(1.0f, 1.0f, 0.0f)),
        };
        _vertexBuffer = Device.CreateBuffer(quadVertices, BindFlags.VertexBuffer);

        ReadOnlySpan<ushort> quadIndices = stackalloc ushort[] {
            0,
            1,
            2,
            0,
            1,
            2,
            0,
            2,
            3
        };
        _indexBuffer = Device.CreateBuffer(quadIndices, BindFlags.IndexBuffer);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPosition2DColor.InputElements, vertexShaderByteCode.Span);
    }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            _vertexBuffer.Dispose();
            _vertexShader.Dispose();
            _pixelShader.Dispose();
            _inputLayout.Dispose();
        }

        base.Dispose(dispose);
    }

    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);

        // Bind Vertex buffers and index buffer
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPosition2DColor.SizeInBytes, 0);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);

        // Triangle
        DeviceContext.DrawIndexed(3, 0, 0);

        // Quad
        bool secondWay = false;
        if (secondWay)
        {
            DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPosition2DColor.SizeInBytes, 3 * VertexPosition2DColor.SizeInBytes);
            DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 3 * sizeof(ushort));
            DeviceContext.DrawIndexed(6, 0, 0);
        }
        else
        {
            DeviceContext.DrawIndexed(6, 3, 3);
        }
    }

    public static void Main()
    {
        using BufferOffsetsApp app = new();
        app.Run();
    }
}
