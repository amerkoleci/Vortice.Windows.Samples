// Copyright (c) Amer Koleci and contributors.
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
using System.Drawing;
using System.Reflection;

namespace Vortice.Framework;

internal sealed unsafe class Win32AppPlatform : AppPlatform
{
    internal const string WindowClassName = "VorticeWindow";
    internal readonly win32.FreeLibrarySafeHandle HInstance;
    private static readonly Dictionary<nint, Win32AppWindow> _windows = [];
    private readonly Win32AppWindow _mainWindow;

    public Win32AppPlatform()
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

        _mainWindow = new Win32AppWindow(this, GetDefaultTitleName(), new(1280, 720));
        _windows.Add(_mainWindow.Handle, _mainWindow);
    }

    public override bool IsBlockingRun => true;
    public override AppWindow MainWindow => _mainWindow;

    // <inheritdoc />
    public override void Run()
    {
        _mainWindow!.Show();

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
                OnTick();
            }
        }

        CoUninitialize();
        OnExiting();
    }

    public override void RequestExit()
    {
        PostQuitMessage(0);
    }

    private static bool s_fullscreen;

    private static void OnKey(uint message, nuint wParam, nint lParam)
    {
        if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            Application.Current?.OnPlatformKeyboardEvent(ConvertKeyCode(lParam, wParam), true);
        else if (message == WM_KEYUP || message == WM_SYSKEYUP)
            Application.Current?.OnPlatformKeyboardEvent(ConvertKeyCode(lParam, wParam), false);
    }

    private static KeyboardKey ConvertKeyCode(nint lParam, nuint wParam)
    {
        uint uWParam = (uint)wParam;
        switch ((VIRTUAL_KEY)uWParam)
        {
            // virtual key codes
            case VK_CLEAR: return KeyboardKey.Clear;
            case VK_MODECHANGE: return KeyboardKey.ModeChange;
            case VK_SELECT: return KeyboardKey.Select;
            case VK_EXECUTE: return KeyboardKey.Execute;
            case VK_HELP: return KeyboardKey.Help;
            case VK_PAUSE: return KeyboardKey.Pause;
            case VK_NUMLOCK: return KeyboardKey.NumLock;

            case VK_F13: return KeyboardKey.F13;
            case VK_F14: return KeyboardKey.F14;
            case VK_F15: return KeyboardKey.F15;
            case VK_F16: return KeyboardKey.F16;
            case VK_F17: return KeyboardKey.F17;
            case VK_F18: return KeyboardKey.F18;
            case VK_F19: return KeyboardKey.F19;
            case VK_F20: return KeyboardKey.F20;
            case VK_F21: return KeyboardKey.F21;
            case VK_F22: return KeyboardKey.F22;
            case VK_F23: return KeyboardKey.F23;
            case VK_F24: return KeyboardKey.F24;

            case VK_OEM_NEC_EQUAL: return KeyboardKey.NumPadEqual;
            case VK_BROWSER_BACK: return KeyboardKey.Back;
            case VK_BROWSER_FORWARD: return KeyboardKey.Forward;
            case VK_BROWSER_REFRESH: return KeyboardKey.Refresh;
            case VK_BROWSER_STOP: return KeyboardKey.Stop;
            case VK_BROWSER_SEARCH: return KeyboardKey.Search;
            case VK_BROWSER_FAVORITES: return KeyboardKey.Bookmarks;
            case VK_BROWSER_HOME: return KeyboardKey.Home;
            case VK_VOLUME_MUTE: return KeyboardKey.Mute;
            case VK_VOLUME_DOWN: return KeyboardKey.VolumeDown;
            case VK_VOLUME_UP: return KeyboardKey.VolumeUp;

            case VK_MEDIA_NEXT_TRACK: return KeyboardKey.AudioNext;
            case VK_MEDIA_PREV_TRACK: return KeyboardKey.AudioPrevious;
            case VK_MEDIA_STOP: return KeyboardKey.AudioStop;
            case VK_MEDIA_PLAY_PAUSE: return KeyboardKey.AudioPlay;
            case VK_LAUNCH_MAIL: return KeyboardKey.Mail;
            case VK_LAUNCH_MEDIA_SELECT: return KeyboardKey.MediaSelect;

            case VK_OEM_102: return KeyboardKey.IntlBackslash;

            case VK_ATTN: return KeyboardKey.PrintScreen;
            case VK_CRSEL: return KeyboardKey.Crsel;
            case VK_EXSEL: return KeyboardKey.Exsel;
            case VK_OEM_CLEAR: return KeyboardKey.Clear;

            case VK_LAUNCH_APP1: return KeyboardKey.App1;
            case VK_LAUNCH_APP2: return KeyboardKey.App2;

            // scan codes
            default:
                {
                    nint scanCode = (lParam >> 16) & 0xFF;
                    if (scanCode <= 127)
                    {
                        bool isExtended = (lParam & (1 << 24)) != 0;

                        switch (scanCode)
                        {
                            case 0x01: return KeyboardKey.Escape;
                            case 0x02: return KeyboardKey.Num1;
                            case 0x03: return KeyboardKey.Num2;
                            case 0x04: return KeyboardKey.Num3;
                            case 0x05: return KeyboardKey.Num4;
                            case 0x06: return KeyboardKey.Num5;
                            case 0x07: return KeyboardKey.Num6;
                            case 0x08: return KeyboardKey.Num7;
                            case 0x09: return KeyboardKey.Num8;
                            case 0x0A: return KeyboardKey.Num9;
                            case 0x0B: return KeyboardKey.Num0;
                            case 0x0C: return KeyboardKey.Minus;
                            case 0x0D: return KeyboardKey.Equal;
                            case 0x0E: return KeyboardKey.Backspace;
                            case 0x0F: return KeyboardKey.Tab;
                            case 0x10: return KeyboardKey.Q;
                            case 0x11: return KeyboardKey.W;
                            case 0x12: return KeyboardKey.E;
                            case 0x13: return KeyboardKey.R;
                            case 0x14: return KeyboardKey.T;
                            case 0x15: return KeyboardKey.Y;
                            case 0x16: return KeyboardKey.U;
                            case 0x17: return KeyboardKey.I;
                            case 0x18: return KeyboardKey.O;
                            case 0x19: return KeyboardKey.P;
                            case 0x1A: return KeyboardKey.LeftBracket;
                            case 0x1B: return KeyboardKey.RightBracket;
                            case 0x1C: return isExtended ? KeyboardKey.NumPadEnter : KeyboardKey.Enter;
                            case 0x1D: return isExtended ? KeyboardKey.RightControl : KeyboardKey.LeftControl;
                            case 0x1E: return KeyboardKey.A;
                            case 0x1F: return KeyboardKey.S;
                            case 0x20: return KeyboardKey.D;
                            case 0x21: return KeyboardKey.F;
                            case 0x22: return KeyboardKey.G;
                            case 0x23: return KeyboardKey.H;
                            case 0x24: return KeyboardKey.J;
                            case 0x25: return KeyboardKey.K;
                            case 0x26: return KeyboardKey.L;
                            case 0x27: return KeyboardKey.Semicolon;
                            case 0x28: return KeyboardKey.Quote;
                            case 0x29: return KeyboardKey.Grave;
                            case 0x2A: return KeyboardKey.LeftShift;
                            case 0x2B: return KeyboardKey.Backslash;
                            case 0x2C: return KeyboardKey.Z;
                            case 0x2D: return KeyboardKey.X;
                            case 0x2E: return KeyboardKey.C;
                            case 0x2F: return KeyboardKey.V;
                            case 0x30: return KeyboardKey.B;
                            case 0x31: return KeyboardKey.N;
                            case 0x32: return KeyboardKey.M;
                            case 0x33: return KeyboardKey.Comma;
                            case 0x34: return KeyboardKey.Period;
                            case 0x35: return isExtended ? KeyboardKey.NumPadDivide : KeyboardKey.Slash;
                            case 0x36: return KeyboardKey.RightShift;
                            case 0x37: return isExtended ? KeyboardKey.PrintScreen : KeyboardKey.NumPadMultiply;
                            case 0x38: return isExtended ? KeyboardKey.RightAlt : KeyboardKey.LeftAlt;
                            case 0x39: return KeyboardKey.Space;
                            case 0x3A: return isExtended ? KeyboardKey.NumPadPlus : KeyboardKey.CapsLock;
                            case 0x3B: return KeyboardKey.F1;
                            case 0x3C: return KeyboardKey.F2;
                            case 0x3D: return KeyboardKey.F3;
                            case 0x3E: return KeyboardKey.F4;
                            case 0x3F: return KeyboardKey.F5;
                            case 0x40: return KeyboardKey.F6;
                            case 0x41: return KeyboardKey.F7;
                            case 0x42: return KeyboardKey.F8;
                            case 0x43: return KeyboardKey.F9;
                            case 0x44: return KeyboardKey.F10;
                            case 0x45: return KeyboardKey.NumLock;
                            case 0x46: return KeyboardKey.ScrollLock;
                            case 0x47: return isExtended ? KeyboardKey.Home : KeyboardKey.NumPad7;
                            case 0x48: return isExtended ? KeyboardKey.Up : KeyboardKey.NumPad8;
                            case 0x49: return isExtended ? KeyboardKey.PageUp : KeyboardKey.NumPad9;
                            case 0x4A: return KeyboardKey.NumPadMinus;
                            case 0x4B: return isExtended ? KeyboardKey.Left : KeyboardKey.NumPad4;
                            case 0x4C: return KeyboardKey.NumPad5;
                            case 0x4D: return isExtended ? KeyboardKey.Right : KeyboardKey.NumPad6;
                            case 0x4E: return KeyboardKey.NumPadPlus;
                            case 0x4F: return isExtended ? KeyboardKey.End : KeyboardKey.NumPad1;
                            case 0x50: return isExtended ? KeyboardKey.Down : KeyboardKey.NumPad2;
                            case 0x51: return isExtended ? KeyboardKey.PageDown : KeyboardKey.NumPad3;
                            case 0x52: return isExtended ? KeyboardKey.Insert : KeyboardKey.NumPad0;
                            case 0x53: return isExtended ? KeyboardKey.Del : KeyboardKey.NumPadDecimal;
                            case 0x54: return KeyboardKey.None;
                            case 0x55: return KeyboardKey.None;
                            case 0x56: return KeyboardKey.IntlBackslash;
                            case 0x57: return KeyboardKey.F11;
                            case 0x58: return KeyboardKey.F12;
                            case 0x59: return KeyboardKey.Pause;
                            case 0x5A: return KeyboardKey.None;
                            case 0x5B: return KeyboardKey.LeftSuper;
                            case 0x5C: return KeyboardKey.RightSuper;
                            case 0x5D: return KeyboardKey.Menu;
                            case 0x5E: return KeyboardKey.None;
                            case 0x5F: return KeyboardKey.None;
                            case 0x60: return KeyboardKey.None;
                            case 0x61: return KeyboardKey.None;
                            case 0x62: return KeyboardKey.None;
                            case 0x63: return KeyboardKey.None;
                            case 0x64: return KeyboardKey.F13;
                            case 0x65: return KeyboardKey.F14;
                            case 0x66: return KeyboardKey.F15;
                            case 0x67: return KeyboardKey.F16;
                            case 0x68: return KeyboardKey.F17;
                            case 0x69: return KeyboardKey.F18;
                            case 0x6A: return KeyboardKey.F19;
                            case 0x6B: return KeyboardKey.None;
                            case 0x6C: return KeyboardKey.None;
                            case 0x6D: return KeyboardKey.None;
                            case 0x6E: return KeyboardKey.None;
                            case 0x6F: return KeyboardKey.None;
                            case 0x70: return KeyboardKey.KatakanaHiragana;
                            case 0x71: return KeyboardKey.None;
                            case 0x72: return KeyboardKey.None;
                            case 0x73: return KeyboardKey.Ro;
                            case 0x74: return KeyboardKey.None;
                            case 0x75: return KeyboardKey.None;
                            case 0x76: return KeyboardKey.None;
                            case 0x77: return KeyboardKey.None;
                            case 0x78: return KeyboardKey.None;
                            case 0x79: return KeyboardKey.Henkan;
                            case 0x7A: return KeyboardKey.None;
                            case 0x7B: return KeyboardKey.Muhenkan;
                            case 0x7C: return KeyboardKey.None;
                            case 0x7D: return KeyboardKey.Yen;
                            case 0x7E: return KeyboardKey.None;
                            case 0x7F: return KeyboardKey.None;
                            default: return KeyboardKey.None;
                        }
                    }
                    else
                        return KeyboardKey.None;
                }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT ProcessWindowMessage(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (_windows.TryGetValue(hWnd, out Win32AppWindow? window))
        {
        }

        switch (message)
        {
            case WM_DESTROY:
                PostQuitMessage(0);
                break;

            case WM_PAINT:
                if (window!.InSizeMove && Application.Current != null)
                {
                    Application.Current.Tick();
                }
                else
                {
                    PAINTSTRUCT ps;
                    _ = BeginPaint(hWnd, &ps);
                    EndPaint(hWnd, &ps);
                }
                break;

            case WM_DISPLAYCHANGE:
                if (Application.Current != null)
                {
                    Application.Current.OnDisplayChange();
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
                    info->ptMinTrackSize.X = 320;
                    info->ptMinTrackSize.Y = 200;
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

            case WM_KEYDOWN:
            case WM_KEYUP:
                OnKey(message, wParam, lParam);
                break;


            case WM_SYSKEYDOWN:
                if ((nuint)wParam == (nuint)VK_RETURN && ((nint)lParam & 0x60000000) == 0x20000000)
                {
                    // Implements the classic ALT+ENTER fullscreen toggle
                    if (s_fullscreen)
                    {
                        Win32Native.SetWindowLongPtr(hWnd, GWL_STYLE, (uint)WS_OVERLAPPEDWINDOW);
                        Win32Native.SetWindowLongPtr(hWnd, GWL_EXSTYLE, 0);

                        SizeI windowSize = new(800, 600);
                        if (Application.Current != null)
                        {
                            windowSize = Application.Current.DefaultSize;
                        }

                        ShowWindow(hWnd, SW_SHOWNORMAL);
                        SetWindowPos(hWnd, HWND.HWND_TOP, 0, 0, windowSize.Width, windowSize.Height, SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
                    }
                    else
                    {
                        Win32Native.SetWindowLongPtr(hWnd, GWL_STYLE, (uint)WS_POPUP);
                        Win32Native.SetWindowLongPtr(hWnd, GWL_EXSTYLE, (uint)WS_EX_TOPMOST);

                        SetWindowPos(hWnd, HWND.HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                        ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                    }

                    s_fullscreen = !s_fullscreen;
                }
                break;

            case WM_MENUCHAR:
                // A menu is active and the user presses a key that does not correspond
                // to any mnemonic or accelerator key. Ignore so we don't produce an error beep.
                return Win32Native.MakeLResult(0, MNC_CLOSE);
        }

        return DefWindowProc(hWnd, message, wParam, lParam);
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

partial class AppPlatform
{
    public static AppPlatform Create() => new Win32AppPlatform();
}
