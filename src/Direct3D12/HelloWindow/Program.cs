// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Direct3D12;
using Vortice.Framework;
using Vortice.Mathematics;

static class Program
{
    class HelloWindowApp : D3D12Application
    {
        public HelloWindowApp()
        {
            UseRenderPass = false;
        }

        protected override void OnRender()
        {
            Color4 clearColor = Colors.CornflowerBlue;

            if (UseRenderPass)
            {
                var renderPassDesc = new RenderPassRenderTargetDescription(ColorTextureView,
                    new RenderPassBeginningAccess(new ClearValue(ColorFormat, clearColor)),
                    new RenderPassEndingAccess(RenderPassEndingAccessType.Preserve)
                    );

                RenderPassDepthStencilDescription? depthStencil = default;
                if (DepthStencilView.HasValue)
                {
                    depthStencil = new RenderPassDepthStencilDescription(
                        DepthStencilView.Value,
                        new RenderPassBeginningAccess(new ClearValue(DepthStencilFormat, 1.0f, 0)),
                        new RenderPassEndingAccess(RenderPassEndingAccessType.Discard)
                        );
                }

                CommandList.BeginRenderPass(renderPassDesc, depthStencil);
            }
            else
            {
                CommandList.ClearRenderTargetView(ColorTextureView, clearColor);

                if (DepthStencilView.HasValue)
                {
                    CommandList.ClearDepthStencilView(DepthStencilView.Value, ClearFlags.Depth, 1.0f, 0);
                }
            }
        }
    }

    static void Main()
    {
        using HelloWindowApp app = new();
        app.Run();
    }
}
