// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Vortice.Framework;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexPositionNormal
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexPositionNormal);

    public static InputElementDescription[] InputElements =
    [
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
    ];

    public VertexPositionNormal(in Vector3 position, in Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
}
