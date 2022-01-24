// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using static Vortice.MediaFoundation.MediaFactory;

static class Program
{
    static void Main()
    {
        // Initialize MediaFoundation
        if (MFStartup().Failure)
        {
            return;
        }

        MFShutdown().CheckError();
    }
}
