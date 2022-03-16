// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;

namespace Vortice.Framework;

public abstract class Window
{
    public abstract string Title { get; set; }
    public abstract SizeI ClientSize { get; }
    public abstract IntPtr Handle { get; }


    public event EventHandler? SizeChanged;

    protected virtual void OnSizeChanged()
    {
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }
}
