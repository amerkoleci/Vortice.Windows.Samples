// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;

namespace Vortice.Framework
{
    public abstract class Application : IDisposable
    {
        public event EventHandler<EventArgs>? Disposed;

        public bool IsDisposed { get; private set; }

        protected Application()
        {
        }

        ~Application()
        {
            Dispose(dispose: false);
        }

        public void Dispose()
        {
            Dispose(dispose: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose)
        {
            if (dispose && !IsDisposed)
            {
                //GraphicsDevice?.Dispose();

                Disposed?.Invoke(this, EventArgs.Empty);
                IsDisposed = true;
            }
        }

    }
}
