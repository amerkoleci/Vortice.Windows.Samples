// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.DXGI;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE;
using static Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;

namespace Vortice.Framework;

public sealed unsafe class Win32AppWindow : AppWindow
{
    private readonly HWND _hwnd;
    private bool _inSizeMove;

    internal Win32AppWindow(Win32AppPlatform owner, string title, Size size)
    {
        Title = title;

        RECT rect = new()
        {
            right = size.Width,
            bottom = size.Height
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
            owner.HInstance,
            default);

        // TODO: Change to CreateWindowExW(WS_EX_TOPMOST, L"$

        if (_hwnd.Value == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                    $"Failed to create window class. Error: {Marshal.GetLastWin32Error()}"
                    );
        }

        //SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(g_game.get()));
    }

    public override string Title { get; set; }

    /// <inheritdoc />
    public override SizeF ClientSize
    {
        get
        {
            RECT windowRect;
            GetClientRect(_hwnd, &windowRect);
            return new SizeF(
                MathF.Max(1.0f, windowRect.right - windowRect.left),
                MathF.Max(1.0f, windowRect.bottom - windowRect.top)
                );
        }
    }

    /// <inheritdoc />
    public override Rectangle Bounds
    {
        get
        {
            RECT windowRect;
            GetWindowRect(_hwnd, &windowRect);
            return new Rectangle(
                windowRect.left, windowRect.top,
                windowRect.right - windowRect.left,
                windowRect.bottom - windowRect.top
                );
        }
    }

    public nint Handle => _hwnd;

    public bool InSizeMove => _inSizeMove;

    public void Dispose()
    {
        DestroyWindow(_hwnd);
    }

    public override IDXGISwapChain1 CreateSwapChain(IDXGIFactory2 factory, ComObject deviceOrCommandQueue, Format colorFormat)
    {
        SizeF size = ClientSize;
        Format backBufferFormat = Utilities.ToSwapChainFormat(colorFormat);

        bool isTearingSupported = false;
        using (IDXGIFactory5? factory5 = factory.QueryInterfaceOrNull<IDXGIFactory5>())
        {
            if (factory5 != null)
            {
                isTearingSupported = factory5.PresentAllowTearing;
            }
        }

        SwapChainDescription1 desc = new()
        {
            Width = (int)size.Width,
            Height = (int)size.Height,
            Format = backBufferFormat,
            BufferCount = BackBufferCount,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = isTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        SwapChainFullscreenDescription fullscreenDesc = new()
        {
            Windowed = true
        };

        IDXGISwapChain1 swapChain = factory.CreateSwapChainForHwnd(deviceOrCommandQueue, _hwnd, desc, fullscreenDesc);
        factory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAltEnter);
        return swapChain;
    }

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
    }
}
