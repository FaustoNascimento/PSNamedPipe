using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PSNamedPipe
{
    public abstract class NamedPipe : IDisposable
    {
        private const int HeaderSize = sizeof(int);
        private readonly PipeStream _pipe;
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);
        protected readonly ManualResetEventSlim Connection = new ManualResetEventSlim(false);
        protected bool Disposed;

        protected NamedPipe(PipeStream pipe)
        {
            _pipe = pipe;
        }

        public bool CanRead => _pipe.CanRead;

        public bool CanSeek => _pipe.CanSeek;

        public bool CanTimeout => _pipe.CanTimeout;

        public bool CanWrite => _pipe.CanWrite;

        public int InBufferSize => _pipe.InBufferSize;

        public bool IsAsync => _pipe.IsAsync;

        public virtual bool IsConnected => Connection.IsSet;

        public int OutBufferSize => _pipe.OutBufferSize;

        public PipeTransmissionMode ReadMode => _pipe.ReadMode;

        public SafePipeHandle SafePipeHandle => _pipe.SafePipeHandle;

        public PipeTransmissionMode TransmissionMode => _pipe.TransmissionMode;

        protected async void StartReading()
        {
            while (true)
            {
                if (!IsConnected) break;

                await GetNextMessage().ConfigureAwait(false);
            }
        }

        private async Task GetNextMessage()
        {
            try
            {
                // Get message size
                var bufferMessageSize = await ReadNamedPipe(HeaderSize).ConfigureAwait(false);
                var messageSize = BitConverter.ToInt32(bufferMessageSize, 0);

                // Get actual message
                var message = await ReadNamedPipe(messageSize).ConfigureAwait(false);

                // Post message
                var messageEventArgs = new MessageAvailableEventArgs(message);
                MessageAvailable?.InvokeAsync(this, messageEventArgs);
            }
            catch (DisconnectedException)
            {
                Connection.Reset();
                Disconnected?.InvokeAsync(this);
            }
        }

        private async Task<byte[]> ReadNamedPipe(int count)
        {
            var buffer = new byte[count];
            var bytesRead = await _pipe.ReadAsync(buffer, 0, count).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new DisconnectedException();
            }

            if (bytesRead != count)
            {
                throw new IOException("Number of expected bytes does not match number of read bytes");
            }

            return buffer;
        }

        public virtual Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            return WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public virtual async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Get and write length of message
                var length = BitConverter.GetBytes(count);
                await _pipe.WriteAsync(length, 0, HeaderSize, cancellationToken).ConfigureAwait(false);

                // Write actual message
                await _pipe.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        
        public Subscription Subscribe()
        {
            return new Subscription(this);
        }

        public void Dispose()
        {
            if (Disposed) return;
            
            // Disposing event will fire first followed by a Disconnected event (if pipe was connected).
            // While it could be argued that a Disconnection must happen before the object is disposed of,
            // The Disposing event simply implies that disposing has commenced - not that it has finished.
            // The disconnection happens as a result of the disposal and only after disposal itself has begun,
            // Therefore the order should be Disposing => Disconnected
            Disposing?.Invoke(this, EventArgs.Empty);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            _pipe.Dispose();
            Disposed = true;
        }

        public event EventHandler<MessageAvailableEventArgs> MessageAvailable;

        public event EventHandler Disconnected;

        public event EventHandler Disposing;
    }
}