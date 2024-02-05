// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe readonly struct VertexPosition2DColor
{
    public static readonly int SizeInBytes = sizeof(VertexPosition2DColor);

    public static readonly InputElementDescription[] InputElements = new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
        new InputElementDescription("COLOR", 0, Format.R32G32B32_Float, 8, 0)
    };

    public VertexPosition2DColor(in Vector2 position, in Color3 color)
    {
        Position = position;
        Color = color;
    }

    public readonly Vector2 Position;
    public readonly Color3 Color;
}
