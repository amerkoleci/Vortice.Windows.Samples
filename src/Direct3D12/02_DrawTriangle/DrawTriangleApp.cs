// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

class DrawTriangleApp : D3D12Application
{
    private ID3D12Resource _vertexBuffer;
    private ID3D12RootSignature _rootSignature;
    private ID3D12PipelineState _pipelineState;

    protected override void Initialize()
    {
        Span<VertexPositionColor> triangleVertices =
        [
            new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
            new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
        ];

        UploadBatch.Begin(CommandListType.Direct);
        _vertexBuffer = D3D12ResourceUtils.CreateStaticBuffer(Device, UploadBatch, triangleVertices, ResourceStates.VertexAndConstantBuffer);
        UploadBatch.End(DirectQueue);

        // Create empty root signature first
        RootSignatureFlags rootSignatureFlags =
            RootSignatureFlags.AllowInputAssemblerInputLayout |
            RootSignatureFlags.DenyHullShaderRootAccess |
            RootSignatureFlags.DenyDomainShaderRootAccess |
            RootSignatureFlags.DenyGeometryShaderRootAccess |
            RootSignatureFlags.DenyPixelShaderRootAccess;

        _rootSignature = Device.CreateRootSignature(new RootSignatureDescription1(rootSignatureFlags));

        // Create pipeline
        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "HelloTriangle.hlsl", "VSMain");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "HelloTriangle.hlsl", "PSMain");

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
            RenderTargetFormats = new[] { ColorFormat },
            DepthStencilFormat = DepthStencilFormat,
            SampleDescription = SampleDescription.Default
        };
        _pipelineState = Device.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
    }

    protected override void OnDestroy()
    {
        _vertexBuffer.Dispose();
        _rootSignature.Dispose();
        _pipelineState.Dispose();
    }

    protected override void OnRender()
    {
        Color4 clearColor = new(0.0f, 0.2f, 0.4f, 1.0f);
        CommandList.ClearRenderTargetView(ColorTextureView, clearColor);

        if (DepthStencilView.HasValue)
        {
            CommandList.ClearDepthStencilView(DepthStencilView.Value, ClearFlags.Depth, 1.0f, 0);
        }

        // Set necessary state.
        CommandList.SetGraphicsRootSignature(_rootSignature);
        CommandList.SetPipelineState(_pipelineState);

        int stride = VertexPositionColor.SizeInBytes;
        int vertexBufferSize = 3 * stride;

        CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);


        CommandList.IASetVertexBuffers(0, new VertexBufferView(_vertexBuffer.GPUVirtualAddress, vertexBufferSize, stride));
        CommandList.DrawInstanced(3, 1, 0, 0);
    }

    static void Main()
    {
        DrawTriangleApp app = new();
        app.Run();
    }
}
