// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Vortice.Framework;
internal class Win32GamePlatform : AppPlatform
{
    public Win32GamePlatform(Application application)
        : base(application)
    {
    }

    // <inheritdoc />
    public override Window MainWindow { get; }

    // <inheritdoc />
    public override void Run()
    {
    }

    // <inheritdoc />
    public override void RequestExit()
    {
    }
}

internal partial class AppPlatform
{
    public static AppPlatform Create(Application application)
    {
        return new Win32GamePlatform(application);
    }
}
