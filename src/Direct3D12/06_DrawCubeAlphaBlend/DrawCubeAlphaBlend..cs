// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

unsafe class DrawCubeAlphaBlend : D3D12Application
{
    private ID3D12Resource _vertexBuffer;
    private ID3D12Resource _indexBuffer;
    private void* _cbvData = default;
    private ID3D12Resource _constantBuffer;
    private ID3D12RootSignature _rootSignature;
    private ID3D12PipelineState _pipelineState;

    protected override void Initialize()
    {
        UploadBatch.Begin(CommandListType.Direct);
        (_vertexBuffer, _indexBuffer) = CreateBox(new Vector3(5.0f));
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
            RootSignatureFlags.DenyGeometryShaderRootAccess |
            RootSignatureFlags.DenyPixelShaderRootAccess;

        RootDescriptor1 rootDescriptor1 = new(0, 0, RootDescriptorFlags.DataStaticWhileSetAtExecute);

        _rootSignature = Device.CreateRootSignature(
            new RootSignatureDescription1(rootSignatureFlags,
            [new RootParameter1(RootParameterType.ConstantBufferView, rootDescriptor1, ShaderVisibility.Vertex)])
        );

        // Create pipeline
        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, $"Cube.hlsl", "VSMain");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, $"Cube.hlsl", "PSMain");

        GraphicsPipelineStateDescription psoDesc = new()
        {
            RootSignature = _rootSignature,
            VertexShader = vertexShaderByteCode,
            PixelShader = pixelShaderByteCode,
            InputLayout = new InputLayoutDescription(VertexPositionColor.InputElementsD3D12),
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = RasterizerDescription.CullNone,
            BlendState = BlendDescription.NonPremultiplied,
            DepthStencilState = DepthStencilDescription.Default,
            RenderTargetFormats = [ColorFormat],
            DepthStencilFormat = DepthStencilFormat,
            SampleDescription = SampleDescription.Default
        };
        _pipelineState = Device.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
    }

    protected override void OnDestroy()
    {
        _constantBuffer.Unmap(0);
        _constantBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
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
        //Unsafe.CopyBlock(ref _cbvData[0], ref Unsafe.As<SceneConstantBuffer, byte>(ref _constantBufferData), (uint)sizeof(SceneConstantBuffer));

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
        int stride = VertexPositionColor.SizeInBytes;
        int vertexBufferSize = 24 * stride;
        CommandList.IASetVertexBuffers(0, new VertexBufferView(_vertexBuffer.GPUVirtualAddress, vertexBufferSize, stride));

        // Index Buffer
        int indexBufferSize = 36 * sizeof(ushort);
        CommandList.IASetIndexBuffer(new IndexBufferView(_indexBuffer.GPUVirtualAddress, indexBufferSize, false));

        CommandList.DrawIndexedInstanced(36, 1, 0, 0, 0);
    }

    private (ID3D12Resource, ID3D12Resource) CreateBox(in Vector3 size)
    {
        const int CubeFaceCount = 6;
        List<VertexPositionColor> vertices = new();
        Span<ushort> indices = stackalloc ushort[36];

        Vector3[] faceNormals =
        [
            Vector3.UnitZ,
            new Vector3(0.0f, 0.0f, -1.0f),
            Vector3.UnitX,
            new Vector3(-1.0f, 0.0f, 0.0f),
            Vector3.UnitY,
            new Vector3(0.0f, -1.0f, 0.0f),
        ];

        Color4[] faceColors =
        [
            new(1.0f, 0.0f, 0.0f, 0.4f),
            new(0.0f, 1.0f, 0.0f, 0.4f),
            new(0.0f, 0.0f, 1.0f, 0.4f),
            new(1.0f, 1.0f, 0.0f, 0.4f),
            new(1.0f, 0.0f, 1.0f, 0.4f),
            new(0.0f, 1.0f, 1.0f, 0.4f),
        ];

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

            indices[indicesCount++] = (ushort)(vbase + 0);
            indices[indicesCount++] = (ushort)(vbase + 2);
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

        ID3D12Resource vertexBuffer = CreateStaticBuffer(vertices.ToArray(), ResourceStates.VertexAndConstantBuffer);
        ID3D12Resource indexBuffer = CreateStaticBuffer(indices.ToArray(), ResourceStates.IndexBuffer);

        return (vertexBuffer, indexBuffer);
    }

    static void Main()
    {
        DrawCubeAlphaBlend app = new();
        app.Run();
    }
}
