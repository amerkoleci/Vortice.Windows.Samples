// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using win32 = global::Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.System.Com.COINIT;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.PEEK_MESSAGE_REMOVE_TYPE;
using static Windows.Win32.UI.WindowsAndMessaging.WNDCLASS_STYLES;
using static Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Gdi;
using static Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE;
using static Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS;
using Vortice.Mathematics;

namespace Vortice.Framework;

internal unsafe class Win32AppPlatform : AppPlatform
{
    public const string WindowClassName = "VorticeWindow";
    public readonly win32.FreeLibrarySafeHandle HInstance;
    private static readonly Dictionary<IntPtr, Win32Window> _windows = new();
    private readonly Win32Window _mainWindow;

    public Win32AppPlatform(Application application)
        : base(application)
    {
        CoInitializeEx(null, COINIT_APARTMENTTHREADED);

#nullable disable
        HInstance = GetModuleHandle((string)null);
#nullable restore

        fixed (char* lpszClassName = WindowClassName)
        {
            PCWSTR szCursorName = new((char*)IDC_ARROW);

            var wndClassEx = new WNDCLASSEXW
            {
                cbSize = (uint)Unsafe.SizeOf<WNDCLASSEXW>(),
                style = CS_HREDRAW | CS_VREDRAW | CS_OWNDC,
                lpfnWndProc = &ProcessWindowMessage,
                hInstance = (HINSTANCE)HInstance.DangerousGetHandle(),
                hCursor = LoadCursor(default, szCursorName),
                hbrBackground = default,
                hIcon = default,
                lpszClassName = lpszClassName
            };

            ushort atom = RegisterClassEx(&wndClassEx);

            if (atom == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to register window class. Error: {Marshal.GetLastWin32Error()}"
                    );
            }
        }

        _mainWindow = new Win32Window(this, GetDefaultTitleName());
        _windows.Add(_mainWindow.Handle, _mainWindow);
    }

    // <inheritdoc />
    public override bool IsBlockingRun => true;

    // <inheritdoc />
    public override Window MainWindow => _mainWindow;

    // <inheritdoc />
    public override void Run()
    {
        _mainWindow.Show();

        // Main message loop
        MSG msg = default;
        while (WM_QUIT != msg.message)
        {
            if (PeekMessage(out msg, default, 0, 0, PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessage(&msg);
            }
            else
            {
                Application.Tick();
            }
        }

        CoUninitialize();
    }

    // <inheritdoc />
    public override void RequestExit()
    {
    }

    private static bool s_fullscreen;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static LRESULT ProcessWindowMessage(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (_windows.TryGetValue(hWnd, out Win32Window? window))
        {
        }

        switch (message)
        {
            case WM_DESTROY:
                PostQuitMessage(0);
                break;

            case WM_PAINT:
                if (window!.InSizeMove && D3D12Application.Current != null)
                {
                    D3D12Application.Current.Tick();
                }
                else
                {
                    PAINTSTRUCT ps;
                    _ = BeginPaint(hWnd, &ps);
                    EndPaint(hWnd, &ps);
                }
                break;

            case WM_DISPLAYCHANGE:
                if (D3D12Application.Current != null)
                {
                    D3D12Application.Current.OnDisplayChange();
                }
                break;

            case WM_MOVE:
                break;

            case WM_SIZE:
                //if ((nuint)wParam == SIZE_MINIMIZED)
                //{
                //    if (!s_minimized)
                //    {
                //        s_minimized = true;
                //        if (!s_in_suspend && game)
                //            game->OnSuspending();
                //        s_in_suspend = true;
                //    }
                //}
                //else if (s_minimized)
                //{
                //    s_minimized = false;
                //    if (s_in_suspend && game)
                //        game->OnResuming();
                //    s_in_suspend = false;
                //}
                //else if (!s_in_sizemove && game)
                //{
                //    game->OnWindowSizeChanged(LOWORD(lParam), HIWORD(lParam));
                //}
                break;

            case WM_ENTERSIZEMOVE:
                window!.EnterSizeMove();
                break;

            case WM_EXITSIZEMOVE:
                window!.ExitSizeMove();
                break;

            case WM_GETMINMAXINFO:
                if ((nint)lParam != 0)
                {
                    MINMAXINFO* info = (MINMAXINFO*)((nint)lParam);
                    info->ptMinTrackSize.x = 320;
                    info->ptMinTrackSize.y = 200;
                }
                break;

            case WM_ACTIVATEAPP:
                //if (game)
                //{
                //    if ((nuint)wParam != 0)
                //    {
                //        game->OnActivated();
                //    }
                //    else
                //    {
                //        game->OnDeactivated();
                //    }
                //}
                break;


            case WM_SYSKEYDOWN:
                if ((nuint)wParam == (nuint)VK_RETURN && ((nint)lParam & 0x60000000) == 0x20000000)
                {
                    // Implements the classic ALT+ENTER fullscreen toggle
                    if (s_fullscreen)
                    {
                        SetWindowLongPtr(hWnd, GWL_STYLE, (uint)WS_OVERLAPPEDWINDOW);
                        SetWindowLongPtr(hWnd, GWL_EXSTYLE, IntPtr.Zero);

                        SizeI windowSize = new(800, 600);
                        if (D3D12Application.Current != null)
                        {
                            windowSize = D3D12Application.Current.DefaultSize;
                        }

                        ShowWindow(hWnd, SW_SHOWNORMAL);
                        SetWindowPos(hWnd, HWND_TOP, 0, 0, windowSize.Width, windowSize.Height, SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
                    }
                    else
                    {
                        SetWindowLongPtr(hWnd, GWL_STYLE, (uint)WS_POPUP);
                        SetWindowLongPtr(hWnd, GWL_EXSTYLE, (uint)WS_EX_TOPMOST);

                        SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                        ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                    }

                    s_fullscreen = !s_fullscreen;
                }
                break;

            case WM_MENUCHAR:
                // A menu is active and the user presses a key that does not correspond
                // to any mnemonic or accelerator key. Ignore so we don't produce an error beep.
                return MakeLResult(0, MNC_CLOSE);
        }

        return DefWindowProc(hWnd, message, wParam, lParam);
    }

    private static int Loword(int number)
    {
        return number & 0x0000FFFF;
    }

    private static int Hiword(int number)
    {
        return number >> 16;
    }

    private static LRESULT MakeLResult(uint lowPart, uint highPart)
    {
        return new LRESULT((nint)((lowPart & 0xffff) | ((highPart & 0xffff) << 16)));
    }

    [DllImport("User32", ExactSpelling = true, EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong_x86(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex);

    [DllImport("User32", ExactSpelling = true, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrImpl_x64(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex);

    public static IntPtr GetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
    {
        return IntPtr.Size == 4 ? (IntPtr)GetWindowLong_x86(hWnd, nIndex) : GetWindowLongPtrImpl_x64(hWnd, nIndex);
    }

    [DllImport("User32", ExactSpelling = true, EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong_x86(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, int dwNewLong);

    [DllImport("User32", ExactSpelling = true, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr_x64(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, uint value)
    {
        return IntPtr.Size == 4 ? (IntPtr)SetWindowLong_x86(hWnd, nIndex, (int)value) : SetWindowLongPtr_x64(hWnd, nIndex, new IntPtr(value));
    }

    private static IntPtr SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 4 ? (IntPtr)SetWindowLong_x86(hWnd, nIndex, (int)dwNewLong) : SetWindowLongPtr_x64(hWnd, nIndex, dwNewLong);
    }
}

internal partial class AppPlatform
{
    public static AppPlatform Create(Application application)
    {
        return new Win32AppPlatform(application);
    }
}
