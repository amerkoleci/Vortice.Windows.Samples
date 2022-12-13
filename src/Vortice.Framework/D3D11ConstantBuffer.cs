// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Vortice.Direct3D11;
using Vortice.Mathematics;

namespace Vortice.Framework;

public sealed unsafe class D3D11ConstantBuffer<T> : DisposableObject
    where T : unmanaged
{
    public ID3D11Buffer Buffer { get; }

    public D3D11ConstantBuffer(ID3D11Device device, string? name = default)
    {
        Guard.IsNotNull(device, nameof(device));

        BufferDescription description = new()
        {
            ByteWidth = (int)MathHelper.AlignUp((uint)sizeof(T), 16),
            BindFlags = BindFlags.ConstantBuffer,
            CPUAccessFlags = CpuAccessFlags.Write,
            Usage = ResourceUsage.Dynamic
        };

        Buffer = device.CreateBuffer(description);

        if (string.IsNullOrEmpty(name) == false)
        {
            Buffer.DebugName = name;
        }
    }

    // <summary>
    /// Finalizes an instance of the <see cref="D3D11ConstantBuffer" /> class.
    /// </summary>
    ~D3D11ConstantBuffer() => Dispose(disposing: false);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Buffer.Dispose();
        }
    }

    public void SetData(ID3D11DeviceContext deviceContext, T data)
    {
        MappedSubresource mappedResource = deviceContext.Map(Buffer, MapMode.WriteDiscard);
        Unsafe.Copy(mappedResource.DataPointer.ToPointer(), ref data);
        deviceContext.Unmap(Buffer, 0);
    }

    public void SetData(ID3D11DeviceContext deviceContext, ref T data)
    {
        MappedSubresource mappedResource = deviceContext.Map(Buffer, MapMode.WriteDiscard);
        Unsafe.Copy(mappedResource.DataPointer.ToPointer(), ref data);
        deviceContext.Unmap(Buffer, 0);
    }

    public static implicit operator ID3D11Buffer(D3D11ConstantBuffer<T> buffer) => buffer.Buffer;
}
