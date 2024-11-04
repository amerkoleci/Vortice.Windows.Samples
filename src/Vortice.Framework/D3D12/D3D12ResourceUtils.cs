//--------------------------------------------------------------------------------------
// File: BufferHelpers.h
//
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// http://go.microsoft.com/fwlink/?LinkID=615561
//--------------------------------------------------------------------------------------

// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.
// Port of DirectXTK12 BufferHelpers

using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Vortice.Direct3D12.D3D12;

namespace Vortice.Framework;

public static unsafe class D3D12ResourceUtils
{
    private const ResourceStates c_initialCopyTargetState = ResourceStates.Common;
    private const ResourceStates c_initialReadTargetState = ResourceStates.Common;
    private const ResourceStates c_initialUAVTargetState = ResourceStates.Common;

    public static ID3D12Resource CreateStaticBuffer<T>(
        ID3D12Device device,
        D3D12ResourceUploadBatch resourceUpload,
        T[] data, ResourceStates afterState,
        ResourceFlags flags = ResourceFlags.None)
        where T : unmanaged
    {
        Span<T> span = data;
        return CreateStaticBuffer(device, resourceUpload, span, afterState, flags);
    }

    public static ID3D12Resource CreateStaticBuffer<T>(
        ID3D12Device device,
        D3D12ResourceUploadBatch resourceUpload,
        Span<T> data,
        ResourceStates afterState,
        ResourceFlags flags = ResourceFlags.None)
        where T : unmanaged
    {
        uint sizeInBytes = (uint)(sizeof(T) * data.Length);

        uint c_maxBytes = RequestResourceSizeInMegaBytesExpressionATerm * 1024u * 1024u;

        if (sizeInBytes > c_maxBytes)
        {
            throw new InvalidOperationException($"ERROR: Resource size too large for DirectX 12 (size {sizeInBytes})");
        }

        ID3D12Resource buffer = device.CreateCommittedResource(
            HeapType.Default,
            HeapFlags.None,
            ResourceDescription.Buffer(sizeInBytes, flags),
            c_initialCopyTargetState
        );

        fixed (T* dataPtr = data)
        {
            SubresourceData initData = new()
            {
                pData = dataPtr,
            };

            resourceUpload.Upload(buffer, 0, &initData, 1);
            resourceUpload.Transition(buffer, ResourceStates.CopyDest, afterState);

            return buffer;
        }
    }

    public static ID3D12Resource CreateUploadBuffer<T>(
        ID3D12Device device,
        T[] data,
        ResourceFlags flags = ResourceFlags.None)
        where T : unmanaged
    {
        uint sizeInBytes = (uint)(sizeof(T) * data.Length);
        fixed (T* dataPtr = data)
        {
            return CreateUploadBuffer(device, sizeInBytes, dataPtr, flags);
        }
    }

    public static ID3D12Resource CreateUploadBuffer<T>(
        ID3D12Device device,
        Span<T> data,
        ResourceFlags flags = ResourceFlags.None)
        where T : unmanaged
    {
        uint sizeInBytes = (uint)(sizeof(T) * data.Length);
        fixed (T* dataPtr = data)
        {
            return CreateUploadBuffer(device, sizeInBytes, dataPtr, flags);
        }
    }

    public static ID3D12Resource CreateUploadBuffer(
        ID3D12Device device,
        uint sizeInBytes,
        void* data = default,
        ResourceFlags flags = ResourceFlags.None)
    {
        uint c_maxBytes = RequestResourceSizeInMegaBytesExpressionATerm * 1024u * 1024u;

        if (sizeInBytes > c_maxBytes)
        {
            throw new InvalidOperationException($"ERROR: Resource size too large for DirectX 12 (size {sizeInBytes})");
        }

        ID3D12Resource buffer = device.CreateCommittedResource(
            HeapType.Upload,
            HeapFlags.None,
            ResourceDescription.Buffer(sizeInBytes, flags),
            ResourceStates.GenericRead
        );

        if (data is not null)
        {
            void* mappedPtr = default;
            buffer.Map(0, null, &mappedPtr).CheckError();
            NativeMemory.Copy(data, mappedPtr, sizeInBytes);
            buffer.Unmap(0, null);
        }

        return buffer;
    }

    public static ID3D12Resource CreateUAVBuffer(ID3D12Device device, uint bufferSize,
        ResourceStates initialState = ResourceStates.Common,
        ResourceFlags flags = ResourceFlags.None)
    {
        uint c_maxBytes = RequestResourceSizeInMegaBytesExpressionATerm * 1024u * 1024u;

        if (bufferSize > c_maxBytes)
        {
            throw new InvalidOperationException($"ERROR: Resource size too large for DirectX 12 (size {bufferSize})");
        }

        ID3D12Resource buffer = device.CreateCommittedResource(
            HeapType.Default,
            HeapFlags.None,
            ResourceDescription.Buffer(bufferSize, ResourceFlags.AllowUnorderedAccess | flags),
            c_initialCopyTargetState
        );

        return buffer;
    }

    public static ID3D12Resource CreateTexture2D<T>(
        ID3D12Device device,
        D3D12ResourceUploadBatch resourceUpload,
        uint width, uint height, Format format,
        Span<T> data,
        bool generateMips = false,
        ResourceStates afterState = ResourceStates.PixelShaderResource,
        ResourceFlags flags = ResourceFlags.None)
        where T : unmanaged
    {
        if ((width > RequestTexture2DUOrVDimension) || (height > RequestTexture2DUOrVDimension))
        {
            throw new InvalidOperationException($"ERROR: Resource dimensions too large for DirectX 12 (2D: size {width} by {height})");
        }

        ushort mipLevels = 1;
        if (generateMips)
        {
            generateMips = resourceUpload.IsSupportedForGenerateMips(format);
            if (generateMips)
            {
                mipLevels = (ushort)Utilities.CountMips(width, height);
            }
        }


        ID3D12Resource texture = device.CreateCommittedResource(
            HeapType.Default,
            HeapFlags.None,
            ResourceDescription.Texture2D(format, width, height, 1, mipLevels, 1, 0, flags),
            c_initialCopyTargetState
        );

        fixed (T* dataPtr = data)
        {
            FormatHelper.GetSurfaceInfo(format, width, height, out uint rowPitch, out uint slicePitch);
            SubresourceData initData = new()
            {
                pData = dataPtr,
                RowPitch = (nint)rowPitch,
                SlicePitch = (nint)slicePitch
            };

            resourceUpload.Upload(texture, 0, &initData, 1);
            resourceUpload.Transition(texture, ResourceStates.CopyDest, afterState);

            if (generateMips)
            {
                resourceUpload.GenerateMips(texture);
            }

            return texture;
        }
    }
}
