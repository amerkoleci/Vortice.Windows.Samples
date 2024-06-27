// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

public sealed class DrawQuadApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;
    private readonly Random _random = new();
    private readonly bool _dynamicBuffer = true;

    protected override unsafe void Initialize()
    {
        ReadOnlySpan<VertexPositionColor> source =
        [
            new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f)),
            new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
        ];
        if (_dynamicBuffer)
        {
            _vertexBuffer = Device.CreateBuffer(source.Length * VertexPositionColor.SizeInBytes, BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
            // It can be updated in this way
            // MappedSubresource mappedResource = DeviceContext.Map(_vertexBuffer, MapMode.WriteDiscard);
            // source.CopyTo(new Span<VertexPositionColor>(mappedResource.DataPointer.ToPointer(), source.Length));
            // DeviceContext.Unmap(_vertexBuffer, 0);

            // Or with helper method
            _vertexBuffer.SetData(DeviceContext, source, MapMode.WriteDiscard);
        }
        else
        {
            _vertexBuffer = Device.CreateBuffer(source, BindFlags.VertexBuffer);
        }

        ReadOnlySpan<ushort> quadIndices = [0, 1, 2, 0, 2, 3];
        _indexBuffer = Device.CreateBuffer(quadIndices, BindFlags.IndexBuffer);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPositionColor.InputElements, vertexShaderByteCode.Span);
    }

    protected override void OnDestroy()
    {
        _vertexBuffer.Dispose();
        _vertexShader.Dispose();
        _pixelShader.Dispose();
        _inputLayout.Dispose();
    }

    protected override void OnRender()
    {
        if (_dynamicBuffer)
        {
            ReadOnlySpan<VertexPositionColor> source =
            [
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.0f), RandomColor()),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), RandomColor()),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
            ];

            _vertexBuffer.SetData(DeviceContext, source, MapMode.WriteDiscard);
        }

        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionColor.SizeInBytes);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
        DeviceContext.DrawIndexed(6, 0, 0);
    }

    private Color4 RandomColor()
    {
        return new Color4((float)_random.NextDouble(), (float)_random.NextDouble(), (float)_random.NextDouble(), 1.0f);
    }

    public static void Main()
    {
        DrawQuadApp app = new();
        app.Run();
    }
}
