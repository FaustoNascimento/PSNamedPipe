using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PSNamedPipe
{
    public class NamedPipeServer : NamedPipe
    {
        private readonly SemaphoreSlim _connectSemaphore = new SemaphoreSlim(1);
        private readonly NamedPipeServerStream _pipe;

        protected NamedPipeServer(NamedPipeServerStream pipe) : base(pipe)
        {
            _pipe = pipe;
        }

        public static NamedPipeServer Create(string pipeName, PipeDirection direction = PipeDirection.InOut,
            int maxNumberOfServerInstances = 1, int inBufferSize = 1024, int outBufferSize = 1024, bool autoReconnect = true)
        {
            var pipe = new NamedPipeServerStream(pipeName, direction, maxNumberOfServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, inBufferSize, outBufferSize);
            var wrapper = new NamedPipeServer(pipe);
            
            return wrapper;
        }

        public async Task Start(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _connectSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await _pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                Connection.Set();
                Connected?.InvokeAsync(this);
                StartReading();
            }
            finally
            {
                _connectSemaphore.Release();
            }
        }

        public async Task<Subscription> StartAndSubscribe(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            await Start(cancellationToken);
            return subscription;
        }

        public void Disconnect()
        {
            Connection.Reset();
            _pipe.Disconnect();
        }

        public event EventHandler Connected;
    }
}