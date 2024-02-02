// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Microsoft.UI;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Vortice.Framework;

internal class UWPAppPlatform : AppPlatform, IFrameworkViewSource
{
    private UWPWindow _mainWindow;

    public UWPAppPlatform(Application application)
        : base(application)
    {
        _mainWindow = new UWPWindow(this, GetDefaultTitleName());
    }

    // <inheritdoc />
    public override bool IsBlockingRun => false;

    // <inheritdoc />
    public override Window MainWindow => _mainWindow;

    IFrameworkView IFrameworkViewSource.CreateView() => _mainWindow;

    public static void ExtendViewIntoTitleBar(bool extendViewIntoTitleBar)
    {
        CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = extendViewIntoTitleBar;

        if (extendViewIntoTitleBar)
        {
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    // <inheritdoc />
    public override void Run()
    {
        CoreApplication.Run(this);
    }

    // <inheritdoc />
    public override void RequestExit()
    {
        //ExitRequested = true;
        //OnExiting();
        CoreApplication.Exit();
        //Application.Current.Exit();
    }

    public void Activate()
    {
        OnActivated();
    }

    public void Resume()
    {
        //OnResume();
    }

    public void Suspend()
    {
        //OnSuspend();
    }
}

internal partial class AppPlatform
{
    public static AppPlatform Create(Application application)
    {
        return new UWPAppPlatform(application);
    }
}
