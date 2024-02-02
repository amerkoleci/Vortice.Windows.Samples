// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;

namespace Vortice.Framework;

public abstract partial class Application : IDisposable
{
    private readonly AppPlatform _platform;

    public event EventHandler<EventArgs>? Disposed;

    protected Application()
    {
        _platform = AppPlatform.Create(this);
        //_platform.Activated += GamePlatform_Activated;
        //_platform.Deactivated += GamePlatform_Deactivated;
        _platform.Ready += OnPlatformReady;

        Current = this;
    }

    public static Application? Current { get; private set; }

    public bool IsDisposed { get; private set; }
    public Window MainWindow => _platform.MainWindow;
    public virtual SizeI DefaultSize => new(1280, 720);
    public bool EnableVerticalSync { get; set; } = true;
    public float AspectRatio => (float)MainWindow.ClientSize.Width / MainWindow.ClientSize.Height;

    ~Application()
    {
        Dispose(dispose: false);
    }

    public void Dispose()
    {
        Dispose(dispose: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool dispose)
    {
        if (dispose && !IsDisposed)
        {
            Disposed?.Invoke(this, EventArgs.Empty);
            IsDisposed = true;
        }
    }

    public void Run()
    {
        _platform.Run();

        if (_platform.IsBlockingRun)
        {
        }
    }

    public void Exit()
    {
        _platform.RequestExit();
    }

    internal void Tick()
    {
        if (!BeginDraw())
            return;

        Render();

        EndDraw();
    }

    protected virtual void Initialize()
    {

    }

    protected virtual bool BeginDraw()
    {
        return true;
    }

    protected virtual void EndDraw()
    {
    }

    protected virtual void OnKeyboardEvent(KeyboardKey key, bool pressed)
    {
        if(key == KeyboardKey.Escape && pressed)
        {
            Exit();
        }
    }

    protected internal abstract void Render();

    // Platform events
    internal void OnPlatformReady(object? sender, EventArgs e)
    {
        Initialize();
    }

    internal void OnPlatformKeyboardEvent(KeyboardKey key, bool pressed)
    {
        OnKeyboardEvent(key, pressed);
    }

    internal void OnDisplayChange()
    {

    }
}
