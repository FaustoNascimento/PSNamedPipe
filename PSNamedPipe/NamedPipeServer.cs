using System;
using System.IO.Pipes;

namespace PSNamedPipe
{
    public class NamedPipeServer : NamedPipe
    {
        private readonly NamedPipeServerStream _pipe;
        private bool _connecting;

        protected NamedPipeServer(NamedPipeServerStream pipe) : base(pipe)
        {
            _pipe = pipe;
        }

        public static NamedPipeServer Create(string pipeName, PipeDirection direction = PipeDirection.InOut,
            int maxNumberOfServerInstances = 1, int inBufferSize = 1024, int outBufferSize = 1024, bool autoReconnect = true)
        {
            var pipe = new NamedPipeServerStream(pipeName, direction, maxNumberOfServerInstances,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, inBufferSize, outBufferSize);
            var wrapper = new NamedPipeServer(pipe);
            
            return wrapper;
        }

        public void Start()
        {
            if (_connecting) throw new InvalidOperationException("Already awaiting connection.");

            BeginWaitForConnection();
        }

        public Subscription StartAndSubscribe()
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            Start();
            return subscription;
        }

        public void Disconnect()
        {
            Connection.Reset();
            _pipe.Disconnect();
        }

        protected void BeginWaitForConnection()
        {
            _pipe.BeginWaitForConnection(OnClientConnected, this);
            _connecting = true;
        }

        protected void EndWaitForConnection(IAsyncResult result)
        {
            _pipe.EndWaitForConnection(result);
            _connecting = false;
        }

        protected virtual void OnClientConnected(IAsyncResult result)
        {
            try
            {
                EndWaitForConnection(result);
                Connection.Set();
                BeginRead();

                Connected?.Invoke((NamedPipe) result.AsyncState, EventArgs.Empty);
            }
            catch (ObjectDisposedException)
            {
                // If Dispose() is called while waiting for a connection
                // EndWaitForConnection() will throw ObjectDisposedException
                // So just catch it and do nothing.
            }
        }

        public event EventHandler Connected;
    }
}