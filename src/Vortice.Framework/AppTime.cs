// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using Vortice.Mathematics;

namespace Vortice.Framework;

public class AppTime
{
    public AppTime()
    {
    }

    public AppTime(TimeSpan totalTime, TimeSpan elapsedTime)
    {
        Total = totalTime;
        Elapsed = elapsedTime;
    }

    internal void Update(TimeSpan totalTime, TimeSpan elapsedTime)
    {
        Total = totalTime;
        Elapsed = elapsedTime;
    }

    public TimeSpan Elapsed { get; private set; }

    public TimeSpan Total { get; private set; }
}
