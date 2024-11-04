// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using SharpGen.Runtime;
using Vortice.Direct3D12;
using DescriptorIndex = System.UInt32;

namespace Vortice.Framework;

public class D3D12DescriptorAllocator : IDisposable
{
    private readonly ID3D12Device _device;
    private ID3D12DescriptorHeap? _heap;
    private ID3D12DescriptorHeap? _shaderVisibleHeap;
    private CpuDescriptorHandle _startCpuHandle = default;
    private CpuDescriptorHandle _startCpuHandleShaderVisible = default;
    private GpuDescriptorHandle _startGpuHandleShaderVisible = default;
    private readonly object _mutex = new();
    private bool[] _allocatedDescriptors = [];
    private DescriptorIndex _searchStart;

    private const DescriptorIndex InvalidDescriptorIndex = ~0u;

    public D3D12DescriptorAllocator(ID3D12Device device, DescriptorHeapType type, uint numDescriptors)
    {
        _device = device;
        HeapType = type;
        NumDescriptors = numDescriptors;
        ShaderVisible = (type == DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView) || (type == DescriptorHeapType.Sampler);
        Stride = device.GetDescriptorHandleIncrementSize(type);

        Guard.IsTrue(AllocateResources(numDescriptors));
    }

    public DescriptorHeapType HeapType { get; }
    public uint NumDescriptors { get; private set; }
    public uint NumAllocatedDescriptors { get; private set; }
    public bool ShaderVisible { get; }
    public uint Stride { get; }

    public ID3D12DescriptorHeap Heap => _heap!;
    public ID3D12DescriptorHeap? ShaderVisibleHeap => _shaderVisibleHeap;

    /// <inheritdoc />
    public void Dispose()
    {
        _heap?.Dispose();
        _shaderVisibleHeap?.Dispose();
    }

    public DescriptorIndex AllocateDescriptor() => AllocateDescriptors(1);

    public DescriptorIndex AllocateDescriptors(uint count)
    {
        lock (_mutex)
        {
            DescriptorIndex foundIndex = 0;
            uint freeCount = 0;
            bool found = false;

            // Find a contiguous range of 'count' indices for which _allocatedDescriptors[index] is false
            for (DescriptorIndex index = _searchStart; index < NumDescriptors; index++)
            {
                if (_allocatedDescriptors[index])
                    freeCount = 0;
                else
                    freeCount += 1;

                if (freeCount >= count)
                {
                    foundIndex = index > 0 ? index - count + 1 : 0;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                foundIndex = NumDescriptors;

                if (!Grow(NumDescriptors + count))
                {
                    Debug.WriteLine("ERROR: Failed to grow a descriptor heap!");
                    return InvalidDescriptorIndex;
                }
            }

            for (DescriptorIndex index = foundIndex; index < foundIndex + count; index++)
            {
                _allocatedDescriptors[index] = true;
            }

            NumAllocatedDescriptors += count;
            _searchStart = foundIndex + count;
            return foundIndex;
        }
    }

    public void ReleaseDescriptor(DescriptorIndex index) => ReleaseDescriptors(index, 1);

    public void ReleaseDescriptors(DescriptorIndex baseIndex, uint count = 1)
    {
        if (count == 0)
            return;

        lock (_mutex)
        {
            for (DescriptorIndex index = baseIndex; index < baseIndex + count; index++)
            {
#if DEBUG
                if (!_allocatedDescriptors[index])
                {
                    Debug.WriteLine("Error: Attempted to release an un-allocated descriptor");
                }
#endif

                _allocatedDescriptors[index] = false;
            }

            NumAllocatedDescriptors -= count;

            if (_searchStart > baseIndex)
                _searchStart = baseIndex;
        }
    }

    public CpuDescriptorHandle GetCpuHandle(DescriptorIndex index)
    {
        CpuDescriptorHandle handle = _startCpuHandle;
        return handle.Offset((int)index, Stride);
    }

    public CpuDescriptorHandle GetCpuHandleShaderVisible(DescriptorIndex index)
    {
        CpuDescriptorHandle handle = _startCpuHandleShaderVisible;
        return handle.Offset((int)index, Stride);
    }

    public GpuDescriptorHandle GetGpuHandle(DescriptorIndex index)
    {
        GpuDescriptorHandle handle = _startGpuHandleShaderVisible;
        return handle.Offset((int)index, Stride);
    }

    public void CopyToShaderVisibleHeap(DescriptorIndex index, uint count = 1)
    {
        _device.CopyDescriptorsSimple(count, GetCpuHandleShaderVisible(index), GetCpuHandle(index), HeapType);
    }

    private bool AllocateResources(uint numDescriptors)
    {
        NumDescriptors = numDescriptors;
        _heap?.Dispose();
        _shaderVisibleHeap?.Dispose();

        DescriptorHeapDescription heapDesc = new()
        {
            Type = HeapType,
            DescriptorCount = numDescriptors,
            Flags = DescriptorHeapFlags.None,
            NodeMask = 0
        };

        Result hr = _device.CreateDescriptorHeap(in heapDesc, out _heap);
        if (hr.Failure)
            return false;

        _startCpuHandle = _heap!.GetCPUDescriptorHandleForHeapStart();
        Array.Resize(ref _allocatedDescriptors, (int)numDescriptors);

        if (ShaderVisible)
        {
            heapDesc.Flags = DescriptorHeapFlags.ShaderVisible;

            hr = _device.CreateDescriptorHeap(in heapDesc, out _shaderVisibleHeap);

            if (hr.Failure)
                return false;

            _startCpuHandleShaderVisible = _shaderVisibleHeap!.GetCPUDescriptorHandleForHeapStart();
            _startGpuHandleShaderVisible = _shaderVisibleHeap!.GetGPUDescriptorHandleForHeapStart();
        }

        return true;
    }

    private bool Grow(uint minRequiredSize)
    {
        uint oldSize = NumDescriptors;
        uint newSize = BitOperations.RoundUpToPowerOf2(minRequiredSize);

        ID3D12DescriptorHeap? oldHeap = _heap;

        if (!AllocateResources(newSize))
            return false;

        _device.CopyDescriptorsSimple(oldSize, _startCpuHandle, oldHeap!.GetCPUDescriptorHandleForHeapStart(), HeapType);

        if (_shaderVisibleHeap is not null)
        {
            _device.CopyDescriptorsSimple(oldSize, _startCpuHandleShaderVisible, oldHeap.GetCPUDescriptorHandleForHeapStart(), HeapType);
        }

        return true;
    }
}
