// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Vortice.Framework;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexPositionNormalTexture
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexPositionNormalTexture);

    public static InputElementDescription[] InputElements =>
    [
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
    ];

    public static readonly Direct3D12.InputElementDescription[] InputElementsD3D12 =
    [
        new Direct3D12.InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new Direct3D12.InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
        new Direct3D12.InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
    ];

    public VertexPositionNormalTexture(in Vector3 position, in Vector3 normal, in Vector2 textureCoordinate)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TextureCoordinate;
}
