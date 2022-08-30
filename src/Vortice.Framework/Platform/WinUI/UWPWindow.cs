// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace Vortice.Framework;

internal class UWPWindow : Window, IFrameworkView
{
    private readonly UWPAppPlatform _platform;
    private bool _windowClosed;

    public UWPWindow(UWPAppPlatform platform, string title)
    {
        _platform = platform;
        Title = title;
    }

    public override string Title { get; set; }
    public override SizeI ClientSize { get; }
    public override IntPtr Handle { get; }

    private void OnApplicationViewActivated(CoreApplicationView sender, IActivatedEventArgs e)
    {
        CoreWindow.GetForCurrentThread().Activate();
        _platform.Activate();
    }

    private void OnCoreApplicationResuming(object? sender, object e)
    {
        _platform.Resume();
    }

    private void OnCoreApplicationSuspending(object? sender, SuspendingEventArgs e)
    {
        SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
        _platform.Suspend();
        deferral.Complete();
    }

    void IFrameworkView.Initialize(CoreApplicationView applicationView)
    {
        applicationView.Activated += OnApplicationViewActivated;
        CoreApplication.Resuming += OnCoreApplicationResuming;
        CoreApplication.Suspending += OnCoreApplicationSuspending;
    }

    void IFrameworkView.SetWindow(CoreWindow window)
    {
        window.SizeChanged += OnCoreWindowSizeChanged;
        window.Closed += OnCoreWindowClosed;
        UWPAppPlatform.ExtendViewIntoTitleBar(true);
    }

    private void OnCoreWindowSizeChanged(CoreWindow sender, WindowSizeChangedEventArgs e)
    {
        OnSizeChanged();
    }

    private void OnCoreWindowClosed(CoreWindow sender, CoreWindowEventArgs args)
    {
        _windowClosed = true;
    }

    void IFrameworkView.Load(string entryPoint)
    {
    }

    void IFrameworkView.Run()
    {
        ApplicationView applicationView = ApplicationView.GetForCurrentView();
        //applicationView.Title = title;

        //_platform.OnInit();

        while (!_windowClosed)
        {
            CoreWindow.GetForCurrentThread().Dispatcher.ProcessEvents(CoreProcessEventsOption.ProcessAllIfPresent);

            //_platform.Tick();
        }

        //_platform.Destroy();
    }

    void IFrameworkView.Uninitialize()
    {
    }
}
