// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Vortice.Framework;

internal unsafe static class Win32Native
{
    private static int Loword(int number)
    {
        return number & 0x0000FFFF;
    }

    private static int Hiword(int number)
    {
        return number >> 16;
    }

    public static LRESULT MakeLResult(uint lowPart, uint highPart)
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

    public static IntPtr SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, uint value)
    {
        return IntPtr.Size == 4 ? (IntPtr)SetWindowLong_x86(hWnd, nIndex, (int)value) : SetWindowLongPtr_x64(hWnd, nIndex, new IntPtr(value));
    }

    private static IntPtr SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 4 ? (IntPtr)SetWindowLong_x86(hWnd, nIndex, (int)dwNewLong) : SetWindowLongPtr_x64(hWnd, nIndex, dwNewLong);
    }
}
