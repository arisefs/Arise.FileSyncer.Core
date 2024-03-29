using System;
using System.Threading;
using Arise.FileSyncer.Core.Messages;

namespace Arise.FileSyncer.Core.Components
{
    /// <summary>
    /// Periodically sends a message to check if the connection is still alive.
    /// </summary>
    internal sealed class ConnectionChecker : IDisposable
    {
        private readonly Timer checkerTimer;

        public ConnectionChecker(Action<NetMessage> send, int interval)
        {
            checkerTimer = new Timer(CheckerCallback, send, interval, interval);
        }

        private static void CheckerCallback(object? send)
        {
            if (send == null)
            {
                throw new NullReferenceException("CheckerCallback's send was null");
            }

            ((Action<NetMessage>)send)(new IsAliveMessage());
        }

        #region IDisposable Support
        private bool disposedValue = false;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    checkerTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
