// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;

namespace Vortice.Framework
{
    internal abstract partial class AppPlatform : IDisposable
    {
        private bool _disposed;

        protected AppPlatform(Application application)
        {
            Application = application;
        }

        public Application Application { get; }

        public abstract Window MainWindow { get; }

        public event EventHandler<EventArgs>? Activated;

        public event EventHandler<EventArgs>? Deactivated;

        ~AppPlatform()
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
            if (dispose && !_disposed)
            {
                _disposed = true;
            }
        }

        public abstract void Run();
        public abstract void RequestExit();


        protected void OnActivated()
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }

        protected void OnDeactivated()
        {
            Deactivated?.Invoke(this, EventArgs.Empty);
        }
    }
}
