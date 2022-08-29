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

static class Program
{
    internal class DrawTriangle : D3D11Application
    {
        private ID3D11Buffer _vertexBuffer;
        private ID3D11VertexShader _vertexShader;
        private ID3D11PixelShader _pixelShader;
        private ID3D11InputLayout _inputLayout;

        protected override void Initialize()
        {
            ReadOnlySpan<VertexPositionColor> triangleVertices = stackalloc VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
            };
            _vertexBuffer = Device.CreateBuffer(triangleVertices, BindFlags.VertexBuffer);

            ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "VSMain", "vs_4_0");
            ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode("HelloTriangle.hlsl", "PSMain", "ps_4_0");

            _vertexShader = Device.CreateVertexShader(vertexShaderByteCode.Span);
            _pixelShader = Device.CreatePixelShader(pixelShaderByteCode.Span);

            _inputLayout = Device.CreateInputLayout(VertexPositionColor.InputElements, vertexShaderByteCode.Span);
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
            DeviceContext.IASetVertexBuffer(0, _vertexBuffer, VertexPositionColor.SizeInBytes);
            DeviceContext.Draw(3, 0);
        }

        private static ReadOnlyMemory<byte> CompileBytecode(string shaderName, string entryPoint, string profile)
        {
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "Shaders");
            string shaderFile = Path.Combine(assetsPath, shaderName);
            //string shaderSource = File.ReadAllText(Path.Combine(assetsPath, shaderName));

            return Compiler.CompileFromFile(shaderFile, entryPoint, profile);
        }
    }

    static void Main()
    {
        using DrawTriangle app = new();
        app.Run();
    }
}
