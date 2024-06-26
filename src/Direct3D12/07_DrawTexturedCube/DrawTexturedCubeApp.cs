// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

unsafe class DrawTexturedCubeApp : D3D12Application
{
    private const int TextureWidth = 256;
    private const int TextureHeight = 256;
    private const int TexturePixelSize = 4;

    private ID3D12Resource _vertexBuffer;
    private ID3D12Resource _indexBuffer;
    private ID3D12Resource _constantBuffer;
    private void* _cbvData = default;
    private ID3D12Resource _texture;
    private ID3D12RootSignature _rootSignature;
    private ID3D12PipelineState _pipelineState;

    protected override void Initialize()
    {
        MeshData mesh = MeshUtilities.CreateCube(5.0f);

        UploadBatch.Begin(CommandListType.Direct);
        _vertexBuffer = CreateStaticBuffer(mesh.Vertices, ResourceStates.VertexAndConstantBuffer);
        _indexBuffer = CreateStaticBuffer(mesh.Indices, ResourceStates.IndexBuffer);
        //CreateTexture();
        UploadBatch.End(DirectQueue);

        uint constantBufferSize = MathHelper.AlignUp((uint)sizeof(Matrix4x4), 256u); // D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT
        _constantBuffer = Device.CreateCommittedResource(
            HeapType.Upload,
            ResourceDescription.Buffer(constantBufferSize),
            ResourceStates.GenericRead
            );
        fixed (void** pConstantBufferDataBegin = &_cbvData)
        {
            _constantBuffer.Map(0, pConstantBufferDataBegin).CheckError();
        }

        // Create root signature first
        RootSignatureFlags rootSignatureFlags =
            RootSignatureFlags.AllowInputAssemblerInputLayout |
            RootSignatureFlags.DenyHullShaderRootAccess |
            RootSignatureFlags.DenyDomainShaderRootAccess |
            RootSignatureFlags.DenyGeometryShaderRootAccess;

        RootDescriptor1 rootDescriptor1 = new(0, 0, RootDescriptorFlags.DataStaticWhileSetAtExecute);
        DescriptorRange1 srvRange = new(DescriptorRangeType.ShaderResourceView, 1, 0, flags: DescriptorRangeFlags.DataStatic);

        _rootSignature = Device.CreateRootSignature(new RootSignatureDescription1(rootSignatureFlags,
            [
            new RootParameter1(RootParameterType.ConstantBufferView, rootDescriptor1, ShaderVisibility.Vertex),
            new RootParameter1(new RootDescriptorTable1(srvRange), ShaderVisibility.Pixel)
            ],
            [
                new StaticSamplerDescription(SamplerDescription.PointWrap, ShaderVisibility.Pixel, 0, 0) // SamplerPointWrap in Shader
            ]
        ));

        // Create pipeline
        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "TexturedCube.hlsl", "VSMain");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "TexturedCube.hlsl", "PSMain");

        GraphicsPipelineStateDescription psoDesc = new()
        {
            RootSignature = _rootSignature,
            VertexShader = vertexShaderByteCode,
            PixelShader = pixelShaderByteCode,
            InputLayout = new InputLayoutDescription(VertexPositionNormalTexture.InputElementsD3D12),
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = RasterizerDescription.CullCounterClockwise,
            BlendState = BlendDescription.Opaque,
            DepthStencilState = DepthStencilDescription.Default,
            RenderTargetFormats = [ColorFormat],
            DepthStencilFormat = DepthStencilFormat,
            SampleDescription = SampleDescription.Default
        };
        _pipelineState = Device.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
    }

    private void CreateTexture()
    {
        Span<Color> pixels = [
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

        _texture = CreateTexture2D(TextureWidth, TextureHeight, Format.R8G8B8A8_UNorm, pixels);
    }

    protected override void OnDestroy()
    {
        _constantBuffer.Unmap(0);
        _constantBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _texture.Dispose();
        _rootSignature.Dispose();
        _pipelineState.Dispose();
    }

    protected override void OnRender()
    {
        float deltaTime = (float)Time.Total.TotalSeconds;
        Matrix4x4 world = Matrix4x4.CreateRotationX(deltaTime) * Matrix4x4.CreateRotationY(deltaTime * 2) * Matrix4x4.CreateRotationZ(deltaTime * .7f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 25), new Vector3(0, 0, 0), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 100);
        Matrix4x4 viewProjection = Matrix4x4.Multiply(view, projection);
        Matrix4x4 worldViewProjection = Matrix4x4.Multiply(world, viewProjection);
        Unsafe.Copy(_cbvData, ref worldViewProjection);

        Color4 clearColor = new(0.0f, 0.2f, 0.4f, 1.0f);
        CommandList.ClearRenderTargetView(ColorTextureView, clearColor);

        if (DepthStencilView.HasValue)
        {
            CommandList.ClearDepthStencilView(DepthStencilView.Value, ClearFlags.Depth, 1.0f, 0);
        }

        // Set necessary state.
        CommandList.SetPipelineState(_pipelineState);
        CommandList.SetGraphicsRootSignature(_rootSignature);
        CommandList.SetGraphicsRootConstantBufferView(0, _constantBuffer.GPUVirtualAddress);
        CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        // Vertex Buffer
        int stride = VertexPositionNormalTexture.SizeInBytes;
        int vertexBufferSize = 24 * stride;
        CommandList.IASetVertexBuffers(0, new VertexBufferView(_vertexBuffer.GPUVirtualAddress, vertexBufferSize, stride));

        // Index Buffer
        int indexBufferSize = 36 * sizeof(ushort);
        CommandList.IASetIndexBuffer(new IndexBufferView(_indexBuffer.GPUVirtualAddress, indexBufferSize, false));

        // Draw cube now
        CommandList.DrawIndexedInstanced(36, 1, 0, 0, 0);
    }

    static void Main()
    {
        DrawTexturedCubeApp app = new();
        app.Run();
    }
}
