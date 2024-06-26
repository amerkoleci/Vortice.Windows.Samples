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

unsafe class DrawConstBuffersApp : D3D12Application
{
    private ID3D12DescriptorHeap _cbvHeap;
    private int _cbvDescriptorSize;

    private ID3D12Resource _vertexBuffer;
    private byte* _cbvData = default;
    private SceneConstantBuffer _constantBufferData = default;
    private ID3D12Resource _constantBuffer;
    private ID3D12RootSignature _rootSignature;
    private ID3D12PipelineState _pipelineState;

    protected override void Initialize()
    {
        _cbvHeap = Device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1, DescriptorHeapFlags.ShaderVisible));
        _cbvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);

        Span<VertexPositionColor> triangleVertices =
        [
            new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
        ];

        UploadBatch.Begin(CommandListType.Direct);
        _vertexBuffer = CreateStaticBuffer(triangleVertices, ResourceStates.VertexAndConstantBuffer);
        UploadBatch.End(DirectQueue);

        int constantBufferSize = sizeof(SceneConstantBuffer);
        _constantBuffer = Device.CreateCommittedResource(
            HeapType.Upload,
            ResourceDescription.Buffer(constantBufferSize),
            ResourceStates.GenericRead
            );
        fixed (byte** pConstantBufferDataBegin = &_cbvData)
        {
            _constantBuffer.Map(0, pConstantBufferDataBegin).CheckError();
            Unsafe.CopyBlock(ref _cbvData[0], ref Unsafe.As<SceneConstantBuffer, byte>(ref _constantBufferData), (uint)sizeof(SceneConstantBuffer));
        }

        // Describe and create a constant buffer view.
        ConstantBufferViewDescription cbvDesc = new(_constantBuffer);
        Device.CreateConstantBufferView(cbvDesc, _cbvHeap.GetCPUDescriptorHandleForHeapStart());

        // Create root signature first
        RootSignatureFlags rootSignatureFlags =
            RootSignatureFlags.AllowInputAssemblerInputLayout |
            RootSignatureFlags.DenyHullShaderRootAccess |
            RootSignatureFlags.DenyDomainShaderRootAccess |
            RootSignatureFlags.DenyGeometryShaderRootAccess |
            RootSignatureFlags.DenyPixelShaderRootAccess;

        DescriptorRange1 cbvRange = new(DescriptorRangeType.ConstantBufferView, 1, 0, flags: DescriptorRangeFlags.DataStatic);

        _rootSignature = Device.CreateRootSignature(
            new RootSignatureDescription1(rootSignatureFlags,
            [new RootParameter1(new RootDescriptorTable1(cbvRange), ShaderVisibility.Vertex)])
        );

        // Create pipeline
        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, $"HelloConstBuffers.hlsl", "VSMain");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, $"HelloConstBuffers.hlsl", "PSMain");

        GraphicsPipelineStateDescription psoDesc = new()
        {
            RootSignature = _rootSignature,
            VertexShader = vertexShaderByteCode,
            PixelShader = pixelShaderByteCode,
            InputLayout = new InputLayoutDescription(VertexPositionColor.InputElementsD3D12),
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

    protected override void OnDestroy()
    {
        _cbvHeap.Dispose();
        _constantBuffer.Unmap(0);
        _constantBuffer.Dispose();
        _vertexBuffer.Dispose();
        _rootSignature.Dispose();
        _pipelineState.Dispose();
    }

    protected override void OnRender()
    {
        // Update constant buffer
        const float translationSpeed = 0.005f;
        const float OffsetBounds = 1.25f;

        _constantBufferData.Offset.X += translationSpeed;
        if (_constantBufferData.Offset.X > OffsetBounds)
        {
            _constantBufferData.Offset.X = -OffsetBounds;
        }
        Unsafe.CopyBlock(ref _cbvData[0], ref Unsafe.As<SceneConstantBuffer, byte>(ref _constantBufferData), (uint)sizeof(SceneConstantBuffer));

        Color4 clearColor = new(0.0f, 0.2f, 0.4f, 1.0f);
        CommandList.ClearRenderTargetView(ColorTextureView, clearColor);

        if (DepthStencilView.HasValue)
        {
            CommandList.ClearDepthStencilView(DepthStencilView.Value, ClearFlags.Depth, 1.0f, 0);
        }

        // Set necessary state.
        CommandList.SetGraphicsRootSignature(_rootSignature);
        CommandList.SetPipelineState(_pipelineState);

        CommandList.SetDescriptorHeaps(_cbvHeap);
        CommandList.SetGraphicsRootDescriptorTable(0, _cbvHeap.GetGPUDescriptorHandleForHeapStart());

        int stride = VertexPositionColor.SizeInBytes;
        int vertexBufferSize = 3 * stride;

        CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);


        CommandList.IASetVertexBuffers(0, new VertexBufferView(_vertexBuffer.GPUVirtualAddress, vertexBufferSize, stride));
        CommandList.DrawInstanced(3, 1, 0, 0);
    }

    public struct SceneConstantBuffer
    {
        public Vector4 Offset;
        private unsafe fixed float _padding[60]; // Padding so the constant buffer is 256-byte aligned.
    };

    static void Main()
    {
        DrawConstBuffersApp app = new();
        app.Run();
    }
}
