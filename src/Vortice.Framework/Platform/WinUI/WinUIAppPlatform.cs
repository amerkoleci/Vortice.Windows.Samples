// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Vortice.Framework;

public partial class WinUIAppPlatform : AppPlatform
{
    private readonly WinUIAppWindow _mainWindow;
    private Timer? _shutdownTimer;

    public WinUIAppPlatform(Window window, SwapChainPanel panel)
    {
        _mainWindow = new WinUIAppWindow(window, panel);
        //_mainWindow.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        window.Activated += Window_Activated;
        window.VisibilityChanged += Window_VisibilityChanged;

        // https://learn.microsoft.com/en-us/windows/apps/develop/title-bar
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindowTitleBar m_TitleBar = window.AppWindow.TitleBar;
        }

        window.Content.KeyDown += OnControlKeyDown;
        window.Content.KeyUp += OnControlKeyUp;
        window.Content.CharacterReceived += OnControlCharacterReceived;

        window.Closed += (sender, args) =>
        {
            // Due to an issue (https://discord.com/channels/372137812037730304/663434534087426129/979543152853663744), the WinUI 3 app
            // will keep running after the window is closed if any render thread is still going. To work around this, each loaded shader
            // panel is tracked in a conditional weak table, and then when the window is closed they're all manually stopped by setting
            // their shader runners to null. After this, the app should just be able to close automatically when the panels are done.
            _mainWindow.OnShutdown();
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            OnExiting();

            // For good measure, also start a timer of one second. If the app is still alive by then (ie. if the timer callback is
            // executed at all), then just force the whole process to terminate, and exit with the E_APPLICATION_EXITING error code.
            _shutdownTimer = new Timer(_ => Environment.Exit(unchecked((int)0x8000001A)), this, 1000, Timeout.Infinite);
        };
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
            OnActivated();
        else
            OnDeactivated();
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {

    }

    public override bool IsBlockingRun => false;
    public override AppWindow MainWindow => _mainWindow;

    public override void Run()
    {
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    public override void RequestExit()
    {
        _mainWindow.Window.Close();
        //CompositionTarget.Rendering -= OnCompositionTargetRendering;
    }

    private void OnCompositionTargetRendering(object? sender, object e)
    {
        OnTick();
    }

    private void OnControlKeyDown(object? sender, KeyRoutedEventArgs e)
    {
        KeyboardKey key = ConvertKeyCode(e);
        Application.Current?.OnPlatformKeyboardEvent(key, true);
    }

    private void OnControlKeyUp(object? sender, KeyRoutedEventArgs e)
    {
        KeyboardKey key = ConvertKeyCode(e);
        Application.Current?.OnPlatformKeyboardEvent(key, false);
    }

    private void OnControlCharacterReceived(object? sender, CharacterReceivedRoutedEventArgs args)
    {
        //HandleKeyChar(args.Character);
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

    private static KeyboardKey ConvertKeyCode(KeyRoutedEventArgs args)
    {
        VirtualKey key = args.Key;
        Windows.UI.Core.CorePhysicalKeyStatus keyStatus = args.KeyStatus;

        if (key == VirtualKey.Control)
        {
            if (keyStatus.IsExtendedKey)
            {
                return KeyboardKey.RightControl;
            }

            return KeyboardKey.LeftControl;
        }
        if (key == VirtualKey.Menu)
        {
            if (keyStatus.IsExtendedKey)
            {
                return KeyboardKey.RightAlt;
            }

            return KeyboardKey.LeftAlt;
        }
        if (key == VirtualKey.Shift)
        {
            if (keyStatus.ScanCode == 54)
            {
                return KeyboardKey.RightShift;
            }

            return KeyboardKey.LeftShift;
        }

        if (Enum.TryParse(key.ToString(), out KeyboardKey result))
        {
            return result;
        }

        return KeyboardKey.None;
    }
}


partial class AppPlatform
{
    public static AppPlatform Create() => new WinUIAppPlatform(new Window(), new SwapChainPanel());
}
