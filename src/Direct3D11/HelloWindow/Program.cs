// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Framework;

static class Program
{
    class HelloWindowApp : D3D11Application
    {
    }

    static void Main()
    {
        using HelloWindowApp app = new HelloWindowApp();
        app.Run();
    }
}
