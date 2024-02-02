// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Vortice.Framework;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexPositionColor
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexPositionColor);

    public static readonly InputElementDescription[] InputElements =
    [
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
    ];

    public static readonly Direct3D12.InputElementDescription[] InputElementsD3D12 =
    [
        new Direct3D12.InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new Direct3D12.InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
    ];

    public VertexPositionColor(in Vector3 position, in Color4 color)
    {
        Position = position;
        Color = color;
    }

    public readonly Vector3 Position;
    public readonly Color4 Color;
}
