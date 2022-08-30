// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Framework;
using Vortice.Mathematics;

#nullable disable

static class Program
{
    internal class HelloTexture : D3D12Application
    {
        private ID3D12Resource _vertexBuffer;
        private ID3D12RootSignature _rootSignature;
        private ID3D12PipelineState _pipelineState;

        protected override void Initialize()
        {
            ReadOnlySpan<VertexPositionColor> triangleVertices = stackalloc VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
            };

            // Note: using upload heaps to transfer static data like vert buffers is not 
            // recommended. Every time the GPU needs it, the upload heap will be marshalled 
            // over. Please read up on Default Heap usage. An upload heap is used here for 
            // code simplicity and because there are very few verts to actually transfer.
            _vertexBuffer = Device.CreateCommittedResource(
                HeapType.Upload,
                HeapFlags.None,
                ResourceDescription.Buffer(VertexPositionColor.SizeInBytes * triangleVertices.Length),
                ResourceStates.GenericRead
                );
            _vertexBuffer.SetData(triangleVertices);

            // Create empty root signature first
            _rootSignature = Device.CreateRootSignature(new RootSignatureDescription1(RootSignatureFlags.AllowInputAssemblerInputLayout));

            // Create pipeline
            ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "HelloTexture.hlsl", "VSMain");
            ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "HelloTexture.hlsl", "PSMain");

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

        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                _vertexBuffer.Dispose();
                _rootSignature.Dispose();
                _pipelineState.Dispose();
            }

            base.Dispose(dispose);
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
    }

    static void Main()
    {
        using HelloTexture app = new();
        app.Run();
    }
}
