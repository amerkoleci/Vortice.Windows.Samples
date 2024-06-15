// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using SharpGen.Runtime;
using Vortice.DXGI;

namespace Vortice.Framework;

public abstract class AppWindow
{
    public abstract string Title { get; set; }
    public abstract SizeF ClientSize { get; }
    public abstract Rectangle Bounds { get; }
    public float AspectRatio => (float)ClientSize.Width / ClientSize.Height;
    public int BackBufferCount { get; } = 2;

    public event EventHandler? SizeChanged;

    protected virtual void OnSizeChanged()
    {
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    public abstract IDXGISwapChain1 CreateSwapChain(IDXGIFactory2 factory, ComObject deviceOrCommandQueue, Format colorFormat);
}
