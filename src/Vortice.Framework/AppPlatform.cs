// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Reflection;

namespace Vortice.Framework;

internal abstract partial class AppPlatform : IDisposable
{
    private bool _disposed;

    protected AppPlatform(Application application)
    {
        Application = application;
    }

    public Application Application { get; }

    public abstract bool IsBlockingRun { get; }
    public abstract Window MainWindow { get; }

    public event EventHandler<EventArgs>? Ready;

    public event EventHandler<EventArgs>? Activated;

    public event EventHandler<EventArgs>? Deactivated;

    ~AppPlatform()
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
        if (dispose && !_disposed)
        {
            _disposed = true;
        }
    }

    public abstract void Run();
    public abstract void RequestExit();

    protected void OnReady()
    {
        Ready?.Invoke(this, EventArgs.Empty);
    }

    protected void OnActivated()
    {
        Activated?.Invoke(this, EventArgs.Empty);
    }

    protected void OnDeactivated()
    {
        Deactivated?.Invoke(this, EventArgs.Empty);
    }

    internal static string GetDefaultTitleName()
    {
        string? assemblyTitle = GetAssemblyTitle(Assembly.GetEntryAssembly());
        if (!string.IsNullOrEmpty(assemblyTitle))
        {
            return assemblyTitle!;
        }

        return "Vortice";
    }

    private static string? GetAssemblyTitle(Assembly? assembly)
    {
        if (assembly == null)
        {
            return null;
        }

        AssemblyTitleAttribute? atribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
        if (atribute != null)
        {
            return atribute.Title;
        }

        return null;
    }

}
