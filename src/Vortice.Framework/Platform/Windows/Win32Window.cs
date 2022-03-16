// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE;
using static Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX;
using static Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace Vortice.Framework;

internal unsafe class Win32Window : Window, IDisposable
{
    private readonly Win32AppPlatform _platform;
    private readonly HWND _hwnd;

    public Win32Window(Win32AppPlatform platform)
    {
        _platform = platform;
        Title = "Vortice";

        var rect = new RECT
        {
            right = platform.Application.DefaultSize.Width,
            bottom = platform.Application.DefaultSize.Height
        };

        AdjustWindowRectEx(&rect, WS_OVERLAPPEDWINDOW, false, WS_EX_APPWINDOW);

        _hwnd = CreateWindowEx(WS_EX_APPWINDOW,
            Win32AppPlatform.WindowClassName,
            Title,
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            rect.right - rect.left,
            rect.bottom - rect.top,
            default,
            default,
            platform.HInstance,
            default);

        // TODO: Change to CreateWindowExW(WS_EX_TOPMOST, L"$

        if (_hwnd.Value == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                    $"Failed to create window class. Error: {Marshal.GetLastWin32Error()}"
                    );
        }

        //SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(g_game.get()));
       
        RECT windowRect;
        GetClientRect(_hwnd, &windowRect);
        ClientSize = new(windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
    }

    public void Dispose()
    {
        DestroyWindow(_hwnd);
    }

    public override string Title { get; set; }
    public override SizeI ClientSize { get; }
    public override IntPtr Handle => _hwnd;

    public void Show()
    {
        ShowWindow(_hwnd, SW_NORMAL);
    }
}
