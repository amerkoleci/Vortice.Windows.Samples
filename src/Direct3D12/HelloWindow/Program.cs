// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Framework;
using static Vortice.Direct3D12.D3D12;

static class Program
{
    class HelloWindowApp : D3D12Application
    {
        protected override void OnRender()
        {
        }
    }

    static void Main()
    {
        using HelloWindowApp app = new();
        app.Run();
    }
}
