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
using System.Drawing;

namespace Vortice.Framework;

internal unsafe class Win32Window : Window, IDisposable
{
    private readonly Win32AppPlatform _platform;
    private readonly HWND _hwnd;
    private Size _clientSize;
    private bool _inSizeMove;

    public Win32Window(Win32AppPlatform platform, string title)
    {
        _platform = platform;
        Title = title;

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
        _clientSize = new(windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
    }

    public void Dispose()
    {
        DestroyWindow(_hwnd);
    }

    public override string Title { get; set; }
    public override Size ClientSize => _clientSize;
    public override IntPtr Handle => _hwnd;
    public bool InSizeMove => _inSizeMove;

    public void Show()
    {
        ShowWindow(_hwnd, SW_NORMAL);
    }

    internal void EnterSizeMove()
    {
        _inSizeMove = true;
    }

    internal void ExitSizeMove()
    {
        _inSizeMove = false;
        RECT windowRect;
        GetClientRect(_hwnd, &windowRect);
        _clientSize = new(windowRect.right - windowRect.left, windowRect.bottom - windowRect.top);
    }
}
