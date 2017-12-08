using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PSNamedPipe
{
    public class Subscription : IDisposable
    {
        private bool _disposed;
        private readonly BlockingCollection<byte[]> _queue = new BlockingCollection<byte[]>();
        private readonly NamedPipe _pipe;

        public Subscription(NamedPipe pipe)
        {
            _pipe = pipe;
            _pipe.Disconnected += OnDisconnected;
            _pipe.MessageAvailable += OnMessageAvailable;
        }

        private void OnDisconnected(object sender, EventArgs eventArgs)
        {
            _queue.CompleteAdding();
        }

        public byte[] NextMessage(int timeout = Timeout.Infinite)
        {
            _queue.TryTake(out byte[] message, timeout);
            return message;
        }

        private void OnMessageAvailable(object sender, MessageAvailableEventArgs e)
        {
            _queue.Add(e.Message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            _pipe.MessageAvailable -= OnMessageAvailable;
            _queue.CompleteAdding();
            _queue.Dispose();

            _disposed = true;
        }

        ~Subscription()
        {
            Dispose(false);
        }
    }
}