// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

public class CubeAlphaBlendApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private D3D11ConstantBuffer<Matrix4x4> _constantBuffer;
    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;
    private ID3D11RasterizerState _rasterizerState;
    private ID3D11DepthStencilState _depthStencilState;
    private ID3D11BlendState _blendState;
    private Stopwatch _clock;

    protected override void Initialize()
    {
        (_vertexBuffer, _indexBuffer) = CreateBox(new Vector3(5.0f));

        _constantBuffer = new D3D11ConstantBuffer<Matrix4x4>(Device);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("Cube.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("Cube.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPositionColor.InputElements, vertexShaderByteCode.Span);

        _rasterizerState = Device.CreateRasterizerState(RasterizerDescription.CullNone);
        _depthStencilState = Device.CreateDepthStencilState(DepthStencilDescription.Default);
        _blendState = Device.CreateBlendState(BlendDescription.NonPremultiplied);

        _clock = Stopwatch.StartNew();
    }

    protected override void Dispose(bool dispose)
    {
        if (dispose)
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            _constantBuffer.Dispose();
            _vertexShader.Dispose();
            _pixelShader.Dispose();
            _inputLayout.Dispose();
            _rasterizerState.Dispose();
            _depthStencilState.Dispose();
            _blendState.Dispose();
        }

        base.Dispose(dispose);
    }

    protected unsafe override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, new Color4(0.5f, 0.5f, 0.5f, 1.0f));
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        float time = _clock.ElapsedMilliseconds / 1000.0f;
        Matrix4x4 world = Matrix4x4.CreateRotationX(time) * Matrix4x4.CreateRotationY(time * 2) * Matrix4x4.CreateRotationZ(time * .7f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 25), new Vector3(0, 0, 0), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4, AspectRatio, 0.1f, 100);
        Matrix4x4 viewProjection = Matrix4x4.Multiply(view, projection);
        Matrix4x4 worldViewProjection = Matrix4x4.Multiply(world, viewProjection);

        // Update constant buffer data
        _constantBuffer.SetData(DeviceContext, ref worldViewProjection);

        DeviceContext.RSSetState(_rasterizerState);
        DeviceContext.OMSetDepthStencilState(_depthStencilState);
        DeviceContext.OMSetBlendState(_blendState);

        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.VSSetConstantBuffer(0, _constantBuffer);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionColor.SizeInBytes);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
        

        DeviceContext.DrawIndexed(36, 0, 0);
    }

    private (ID3D11Buffer, ID3D11Buffer) CreateBox(in Vector3 size)
    {
        const int CubeFaceCount = 6;
        List<VertexPositionColor> vertices = new();
        Span<ushort> indices = stackalloc ushort[36];

        Vector3[] faceNormals = new Vector3[CubeFaceCount]
        {
            Vector3.UnitZ,
            new Vector3(0.0f, 0.0f, -1.0f),
            Vector3.UnitX,
            new Vector3(-1.0f, 0.0f, 0.0f),
            Vector3.UnitY,
            new Vector3(0.0f, -1.0f, 0.0f),
        };

        Color4[] faceColors = new Color4[CubeFaceCount]
        {
            new(1.0f, 0.0f, 0.0f, 0.4f),
            new(0.0f, 1.0f, 0.0f, 0.4f),
            new(0.0f, 0.0f, 1.0f, 0.4f),
            new(1.0f, 1.0f, 0.0f, 0.4f),
            new(1.0f, 0.0f, 1.0f, 0.4f),
            new(0.0f, 1.0f, 1.0f, 0.4f),
        };

        Vector3 tsize = size / 2.0f;

        // Create each face in turn.
        int vbase = 0;
        int indicesCount = 0;
        for (int i = 0; i < CubeFaceCount; i++)
        {
            Vector3 normal = faceNormals[i];
            Color4 color = faceColors[i];

            // Get two vectors perpendicular both to the face normal and to each other.
            Vector3 basis = (i >= 4) ? Vector3.UnitZ : Vector3.UnitY;

            Vector3 side1 = Vector3.Cross(normal, basis);
            Vector3 side2 = Vector3.Cross(normal, side1);

            // Six indices (two triangles) per face.
            indices[indicesCount++] = (ushort)(vbase + 0);
            indices[indicesCount++] = (ushort)(vbase + 1);
            indices[indicesCount++] = (ushort)(vbase + 2);

            indices[indicesCount++] =(ushort)(vbase + 0);
            indices[indicesCount++] =(ushort)(vbase + 2);
            indices[indicesCount++] = (ushort)(vbase + 3);

            // Four vertices per face.
            // (normal - side1 - side2) * tsize // normal // t0
            vertices.Add(new VertexPositionColor(
                Vector3.Multiply(Vector3.Subtract(Vector3.Subtract(normal, side1), side2), tsize),
                color
                ));

            // (normal - side1 + side2) * tsize // normal // t1
            vertices.Add(new VertexPositionColor(
                Vector3.Multiply(Vector3.Add(Vector3.Subtract(normal, side1), side2), tsize),
                color
                ));

            // (normal + side1 + side2) * tsize // normal // t2
            vertices.Add(new VertexPositionColor(
                Vector3.Multiply(Vector3.Add(normal, Vector3.Add(side1, side2)), tsize),
                color
                ));

            // (normal + side1 - side2) * tsize // normal // t3
            vertices.Add(new VertexPositionColor(
                Vector3.Multiply(Vector3.Subtract(Vector3.Add(normal, side1), side2), tsize),
                color
                ));

            vbase += 4;
        }

        ID3D11Buffer vertexBuffer = Device.CreateBuffer(vertices.ToArray(), BindFlags.VertexBuffer);
        ID3D11Buffer indexBuffer = Device.CreateBuffer(indices.ToArray(), BindFlags.IndexBuffer);

        return (vertexBuffer, indexBuffer);
    }

    static void Main()
    {
        using CubeAlphaBlendApp app = new();
        app.Run();
    }
}
