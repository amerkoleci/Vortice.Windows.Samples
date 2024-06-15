// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Vortice.Framework;

public abstract partial class AppPlatform
{
    protected AppPlatform()
    {
    }

    public abstract bool IsBlockingRun { get; }
    public abstract AppWindow MainWindow { get; }

    public Action? Tick;
    public Action? Exiting;
    public Action? Activated;
    public Action? Deactivated;

    public abstract void Run();
    public abstract void RequestExit();

    protected void OnTick()
    {
        Tick?.Invoke();
    }

    protected void OnExiting()
    {
        Exiting?.Invoke();
    }

    protected void OnActivated()
    {
        Activated?.Invoke();
    }

    protected void OnDeactivated()
    {
        Deactivated?.Invoke();
    }
}
