// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

#nullable disable

using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

public unsafe class TexturedCubeApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private D3D11ConstantBuffer<Matrix4x4> _constantBuffer;
    private ID3D11Texture2D _texture;
    private ID3D11ShaderResourceView _textureSRV;
    private ID3D11SamplerState _textureSampler;

    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;
    private bool _dynamicTexture;

    protected override void Initialize()
    {
        MeshData mesh = MeshUtilities.CreateCube(5.0f);
        _vertexBuffer = Device.CreateBuffer(mesh.Vertices, BindFlags.VertexBuffer);
        _indexBuffer = Device.CreateBuffer(mesh.Indices, BindFlags.IndexBuffer);

        _constantBuffer = new(Device);

        ReadOnlySpan<Color> pixels = [
            0xFFFFFFFF,
            0x00000000,
            0xFFFFFFFF,
            0x00000000,
            0x00000000,
            0xFFFFFFFF,
            0x00000000,
            0xFFFFFFFF,
            0xFFFFFFFF,
            0x00000000,
            0xFFFFFFFF,
            0x00000000,
            0x00000000,
            0xFFFFFFFF,
            0x00000000,
            0xFFFFFFFF,
        ];
        _texture = Device.CreateTexture2D(pixels, Format.R8G8B8A8_UNorm, 4, 4, mipLevels: 1);
        _textureSRV = Device.CreateShaderResourceView(_texture);
        _textureSampler = Device.CreateSamplerState(SamplerDescription.PointWrap);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("Cube.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("Cube.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPositionNormalTexture.InputElements, vertexShaderByteCode.Span);
    }

    protected override void OnDestroy()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _constantBuffer.Dispose();
        _textureSRV.Dispose();
        _textureSampler.Dispose();
        _texture.Dispose();
        _vertexShader.Dispose();
        _pixelShader.Dispose();
        _inputLayout.Dispose();
    }

    protected override void OnKeyboardEvent(KeyboardKey key, bool pressed)
    {
        if (key == KeyboardKey.D && pressed)
        {
            _dynamicTexture = !_dynamicTexture;
        }
    }

    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        float deltaTime = (float)Time.Total.TotalSeconds; 
        Matrix4x4 world = Matrix4x4.CreateRotationX(deltaTime) * Matrix4x4.CreateRotationY(deltaTime * 2) * Matrix4x4.CreateRotationZ(deltaTime * .7f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 25), new Vector3(0, 0, 0), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4, AspectRatio, 0.1f, 100);
        Matrix4x4 viewProjection = Matrix4x4.Multiply(view, projection);
        Matrix4x4 worldViewProjection = Matrix4x4.Multiply(world, viewProjection);

        // Update constant buffer data
        _constantBuffer.SetData(DeviceContext, ref worldViewProjection);

        // Update texture data
        if (_dynamicTexture)
        {
            ReadOnlySpan<Color> pixels = [
                (Color)Colors.Red,
                0x00000000,
                (Color)Colors.Green,
                0x00000000,
                0x00000000,
                (Color)Colors.Blue,
                0x00000000,
                0xFFFFFFFF,
                0xFFFFFFFF,
                0x00000000,
                0xFFFFFFFF,
                0x00000000,
                0x00000000,
                0xFFFFFFFF,
                0x00000000,
                0xFFFFFFFF,
            ];
            DeviceContext.WriteTexture(_texture, 0, 0, pixels);
        }

        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.VSSetConstantBuffer(0, _constantBuffer);
        DeviceContext.PSSetShaderResource(0, _textureSRV);
        DeviceContext.PSSetSampler(0, _textureSampler);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionNormalTexture.SizeInBytes);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
        DeviceContext.DrawIndexed(36, 0, 0);
    }

    public static void Main()
    {
        TexturedCubeApp app = new();
        app.Run();
    }
}
