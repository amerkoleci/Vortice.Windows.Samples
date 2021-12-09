// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;

namespace Vortice.Framework
{
    public abstract class Application : IDisposable
    {
        private readonly AppPlatform _platform;

        public event EventHandler<EventArgs>? Disposed;


        protected Application()
        {
            _platform = AppPlatform.Create(this);
            //_platform.Activated += GamePlatform_Activated;
            //_platform.Deactivated += GamePlatform_Deactivated;
        }

        public bool IsDisposed { get; private set; }
        public Window MainWindow => _platform.MainWindow;

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
