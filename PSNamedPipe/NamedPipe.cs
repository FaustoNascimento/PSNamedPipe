using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PSNamedPipe
{
    public abstract class NamedPipe : IDisposable
    {
        private bool _disposed;
        private readonly PipeStream _pipe;
        private byte[] _buffer;
        private readonly List<byte> _message = new List<byte>();
        protected readonly ManualResetEventSlim Connection = new ManualResetEventSlim(false);

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

        private byte[] Buffer => _buffer ?? (_buffer = new byte[_pipe.InBufferSize]);
        
        protected virtual void BeginRead()
        {
            BeginRead(Buffer, 0, Buffer.Length);
        }

        protected virtual void BeginRead(byte[] buffer, int offset, int count)
        {
            if (IsConnected)
            {
                _pipe.BeginRead(Buffer, offset, count, OnMessageAvailable, this);
            }
        }

        public virtual void WriteAsync(byte[] buffer)
        {
            WriteAsync(buffer, 0, buffer.Length);
        }

        public virtual async void WriteAsync(byte[] buffer, int offset, int count)
        {
            await Task.Run(() => Write(buffer, offset, count));
        }

        public virtual void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public virtual void Write(byte[] buffer, int offset, int count)
        {
            _pipe.Write(buffer, offset, count);
        }

        protected virtual int EndRead(IAsyncResult result)
        {
            var bytesRead = _pipe.EndRead(result);
            return bytesRead;
        }

        public Subscription Subscribe()
        {
            return new Subscription(this);
        }

        public void WaitConnection(int timeout = Timeout.Infinite)
        {
            Connection.Wait(timeout);
        }

        protected virtual void OnMessageAvailable(IAsyncResult result)
        {
            var messageLength = EndRead(result);

            if (messageLength == 0)
            {
                Disconnected?.Invoke(result.AsyncState, EventArgs.Empty);
                return;
            }

            _message.AddRange(_buffer.Take(messageLength));

            var messageComplete = false;
            var endMessage = new byte[0];

            if (_pipe.IsMessageComplete)
            {
                endMessage = _message.ToArray();
                _message.Clear();
                messageComplete = true;
            }

            BeginRead();

            if (!messageComplete) return;

            var messageEventArgs = new MessageAvailableEventArgs(endMessage);
            MessageAvailable?.Invoke(result.AsyncState, messageEventArgs);
        }

        public void Dispose()
        {
            // Disposing event will fire first followed by a Disconnected event (if pipe was connected).
            // While it could be argued that a Disconnection must happen before the object is disposed of,
            // The Disposing event simply implies that disposing has commenced - not that it has finished.
            // The disconnection happens as a result of the disposal and only after disposal itself has begun,
            // Therefore the order should be Disposing => Disconnected
            Disposing?.Invoke(this, EventArgs.Empty);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            Connection.Reset();
            _pipe.Dispose();
            Connection.Dispose();

            _disposed = true;
        }

        ~NamedPipe()
        {
            Dispose(false);
        }

        public event EventHandler<MessageAvailableEventArgs> MessageAvailable;

        public event EventHandler Disconnected;

        public event EventHandler Disposing;
    }
}