// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Direct3D11;
using Vortice.Framework;
using Vortice.Mathematics;

class HelloWindowApp : D3D11Application
{
    protected override void OnRender()
    {
        DeviceContext.ClearRenderTargetView(ColorTextureView, Colors.CornflowerBlue);
        DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
    }

    public static void Main()
    {
        using HelloWindowApp app = new();
        app.Run();
    }
}
