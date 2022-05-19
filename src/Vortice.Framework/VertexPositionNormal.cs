// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Vortice.Framework;

public readonly struct VertexPositionNormal
{
    public static unsafe readonly int SizeInBytes = sizeof(VertexPositionNormal);

    public static InputElementDescription[] InputElements = new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
    };

    public VertexPositionNormal(in Vector3 position, in Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
}
