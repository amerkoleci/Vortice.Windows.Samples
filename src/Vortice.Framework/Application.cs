// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using Vortice.Mathematics;

namespace Vortice.Framework;

public abstract partial class Application
{
    private readonly AppPlatform _platform;
    private readonly object _tickLock = new();

    private readonly Stopwatch _stopwatch = new();

    public AppWindow MainWindow => _platform.MainWindow;
    public virtual SizeI DefaultSize => new(1280, 720);
    public bool EnableVerticalSync { get; set; } = true;
    public float AspectRatio => MainWindow.AspectRatio;
    public bool IsRunning { get; private set; }
    public bool IsExiting { get; private set; }
    public AppTime Time { get; } = new();

    public static Application? Current { get; private set; }

    protected Application(AppPlatform? platform = default)
    {
        _platform = platform ?? AppPlatform.Create();
        _platform.Tick = Tick;
        _platform.Exiting = OnPlatformExiting;

        Current = this;
    }

    protected abstract void OnShutdown();

    public void Run()
    {
        if (IsRunning)
            throw new InvalidOperationException("This application is already running.");

        IsRunning = true;
        Initialize();
        LoadContentAsync();

        _stopwatch.Start();
        Time.Update(_stopwatch.Elapsed, TimeSpan.Zero);

        //BeginRun();
        _platform.Run();

        if (_platform.IsBlockingRun)
        {
            //OnShutdown();
        }
    }

    public void Exit()
    {
        if (IsRunning)
        {
            IsExiting = true;
            _platform.RequestExit();
        }
    }

    private void CheckEndRun()
    {
        if (IsExiting && IsRunning)
        {
            //EndRun();

            _stopwatch.Stop();

            IsRunning = false;
        }
    }

    public void Tick()
    {
        lock (_tickLock)
        {
            if (IsExiting)
            {
                CheckEndRun();
                return;
            }

            try
            {
                TimeSpan elapsedTime = _stopwatch.Elapsed - Time.Total;
                Time.Update(_stopwatch.Elapsed, elapsedTime);

                Update(Time);

                if (BeginDraw())
                {
                    Draw(Time);
                }
            }
            finally
            {
                EndDraw();

                CheckEndRun();
            }
        }
    }

    protected virtual void Initialize()
    {

    }

    protected virtual Task LoadContentAsync()
    {
        return Task.CompletedTask;
    }

    protected virtual void Update(AppTime time)
    {

    }

    protected virtual bool BeginDraw()
    {
        return true;
    }

    protected virtual void Draw(AppTime time)
    {

    }

    protected virtual void EndDraw()
    {
    }

    protected virtual void OnKeyboardEvent(KeyboardKey key, bool pressed)
    {
        if (key == KeyboardKey.Escape && pressed)
        {
            Exit();
        }
    }

    private void OnPlatformExiting()
    {
        OnShutdown();
    }

    internal void OnPlatformKeyboardEvent(KeyboardKey key, bool pressed)
    {
        OnKeyboardEvent(key, pressed);
    }

    internal void OnDisplayChange()
    {

    }
}
