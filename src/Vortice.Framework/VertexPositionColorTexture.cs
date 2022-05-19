// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Vortice.Framework;

public readonly struct VertexPositionColorTexture
{
    public static unsafe readonly int SizeInBytes = sizeof(VertexPositionColorTexture);

    public static InputElementDescription[] InputElements = new[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 28, 0)
    };

    public VertexPositionColorTexture(in Vector3 position, in Color4 color, in Vector2 textureCoordinate)
    {
        Position = position;
        Color = color;
        TextureCoordinate = textureCoordinate;
    }

    public readonly Vector3 Position;
    public readonly Color4 Color;
    public readonly Vector2 TextureCoordinate;
}
