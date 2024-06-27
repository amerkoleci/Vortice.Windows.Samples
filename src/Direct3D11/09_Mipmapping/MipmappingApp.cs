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

public sealed class MipmappingApp : D3D11Application
{
    private ID3D11Buffer _vertexBuffer;
    private ID3D11Buffer _indexBuffer;
    private D3D11ConstantBuffer<Matrix4x4> _constantBuffer;
    private D3D11ConstantBuffer<Vector4> _mipLevelsCB;
    private ID3D11Texture2D _texture;
    private ID3D11ShaderResourceView _textureSRV;
    private ID3D11SamplerState _textureSampler;

    private ID3D11VertexShader _vertexShader;
    private ID3D11PixelShader _pixelShader;
    private ID3D11InputLayout _inputLayout;
    private Stopwatch _mipLevelslock;
    private float _time;
    private int _mipLevel;

    protected override void Initialize()
    {
        MeshData mesh = MeshUtilities.CreateCube(5.0f);
        _vertexBuffer = Device.CreateBuffer(mesh.Vertices, BindFlags.VertexBuffer);
        _indexBuffer = Device.CreateBuffer(mesh.Indices, BindFlags.IndexBuffer);

        _constantBuffer = new(Device);
        _mipLevelsCB = new(Device);

        string assetsPath = Path.Combine(AppContext.BaseDirectory, "Textures");
        string textureFile = Path.Combine(assetsPath, "10points.png");
        (_texture, _textureSRV) = LoadTexture(textureFile);
        _textureSampler = Device.CreateSamplerState(SamplerDescription.LinearClamp);

        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("Cube.hlsl", "VSMain", "vs_4_0");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("Cube.hlsl", "PSMain", "ps_4_0");

        _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
        _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);
        _inputLayout = Device.CreateInputLayout(VertexPositionNormalTexture.InputElements, vertexShaderByteCode.Span);

        _mipLevelslock = Stopwatch.StartNew();
    }

    protected override void OnDestroy()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _constantBuffer.Dispose();
        _mipLevelsCB.Dispose();
        _textureSRV.Dispose();
        _textureSampler.Dispose();
        _texture.Dispose();
        _vertexShader.Dispose();
        _pixelShader.Dispose();
        _inputLayout.Dispose();
    }

    protected unsafe override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

        float deltaTime = (float)Time.Total.TotalSeconds;
        _time += _mipLevelslock.ElapsedMilliseconds / 1000.0f;
        if (_time > 2)
        {
            _time = 0;
            _mipLevelslock.Restart();

            _mipLevel++;

            if (_mipLevel >= _texture.Description.MipLevels)
            {
                _mipLevel = 0;
            }

            Vector4 mipData = new Vector4(_mipLevel, 0.0f, 0.0f, 0.0f);
            _mipLevelsCB.SetData(DeviceContext, ref mipData);
        }

        Matrix4x4 world = Matrix4x4.CreateRotationX(deltaTime) * Matrix4x4.CreateRotationY(deltaTime * 2) * Matrix4x4.CreateRotationZ(deltaTime * .7f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 25), new Vector3(0, 0, 0), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4, AspectRatio, 0.1f, 100);
        Matrix4x4 viewProjection = Matrix4x4.Multiply(view, projection);
        Matrix4x4 worldViewProjection = Matrix4x4.Multiply(world, viewProjection);

        // Update constant buffer data
        _constantBuffer.SetData(DeviceContext, ref worldViewProjection);



        DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        DeviceContext.VSSetShader(_vertexShader);
        DeviceContext.PSSetShader(_pixelShader);
        DeviceContext.IASetInputLayout(_inputLayout);
        DeviceContext.VSSetConstantBuffer(0, _constantBuffer);
        DeviceContext.PSSetConstantBuffer(1, _mipLevelsCB);
        DeviceContext.PSSetShaderResource(0, _textureSRV);
        DeviceContext.PSSetSampler(0, _textureSampler);
        DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionNormalTexture.SizeInBytes);
        DeviceContext.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
        DeviceContext.DrawIndexed(36, 0, 0);
    }

    public static void Main()
    {
        MipmappingApp app = new();
        app.Run();
    }
}
