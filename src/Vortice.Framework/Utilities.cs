// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.CompilerServices;
using Vortice.DXGI;

namespace Vortice.Framework;

public static class Utilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Format ToSwapChainFormat(Format format)
    {
        // FLIP_DISCARD and FLIP_SEQEUNTIAL swapchain buffers only support these formats
        switch (format)
        {
            case Format.R16G16B16A16_Float:
                return Format.R16G16B16A16_Float;

            case Format.B8G8R8A8_UNorm:
            case Format.B8G8R8A8_UNorm_SRgb:
                return Format.B8G8R8A8_UNorm;

            case Format.R8G8B8A8_UNorm:
            case Format.R8G8B8A8_UNorm_SRgb:
                return Format.R8G8B8A8_UNorm;

            case Format.R10G10B10A2_UNorm:
                return Format.R10G10B10A2_UNorm;

            default:
                return Format.B8G8R8A8_UNorm;
        }
    }



    public static uint CountMips(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return 0;

        uint count = 1;
        while (width > 1 || height > 1)
        {
            width >>= 1;
            height >>= 1;
            count++;
        }

        return count;
    }
}
