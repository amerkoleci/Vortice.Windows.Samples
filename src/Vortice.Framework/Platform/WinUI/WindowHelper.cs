// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.
// Based on: https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/Helper/WindowHelper.cs

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Vortice.Framework;

internal static class WindowHelper
{
    public static List<Window> ActiveWindows { get; } = [];

    public static Window CreateWindow()
    {
        Window newWindow = new()
        {
            SystemBackdrop = new MicaBackdrop()
        };
        TrackWindow(newWindow);
        return newWindow;
    }

    public static void TrackWindow(Window window)
    {
        window.Closed += (sender, args) =>
        {
            ActiveWindows.Remove(window);
        };
        ActiveWindows.Add(window);
    }

    public static Microsoft.UI.Windowing.AppWindow GetAppWindow(Window window)
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(window);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
    }

    public static Window? GetWindowForElement(UIElement element)
    {
        if (element.XamlRoot != null)
        {
            foreach (Window window in ActiveWindows)
            {
                if (element.XamlRoot == window.Content.XamlRoot)
                {
                    return window;
                }
            }
        }

        return null;
    }
}
