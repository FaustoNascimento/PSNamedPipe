using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PSNamedPipe
{
    public class NamedPipeClient : NamedPipe
    {
        private readonly SemaphoreSlim _connectSemaphore = new SemaphoreSlim(1);
        private const int ConnectCheckInterval = 50; // same interval as used by NamedPipeClientStream internally
        private readonly NamedPipeClientStream _pipe;

        protected NamedPipeClient(NamedPipeClientStream pipe) : base(pipe)
        {
            _pipe = pipe;
        }

        public int NumberOfServerInstances => _pipe.NumberOfServerInstances;

        public static NamedPipeClient Create(string pipeName, string serverName = ".",
            PipeDirection direction = PipeDirection.InOut)
        {
            var pipe = new NamedPipeClientStream(serverName, pipeName, direction, PipeOptions.Asynchronous);
            var wrapper = new NamedPipeClient(pipe);

            return wrapper;
        }

        public async Task Connect(int timeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken))
        {
            var startTime = Environment.TickCount;

            var success = await _connectSemaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                throw new TimeoutException();
            }

            try
            {
                if (IsConnected) return;

                var elapsed = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Disposed)
                    {
                        throw new ObjectDisposedException("NamedPipeClient");
                    }

                    var waitTime = timeout == Timeout.Infinite ? ConnectCheckInterval : timeout - elapsed;
                    if (waitTime > ConnectCheckInterval)
                    {
                        waitTime = ConnectCheckInterval;
                    }

                    try
                    {
                        // ReSharper disable once MethodSupportsCancellation
                        await _pipe.ConnectAsync(waitTime).ConfigureAwait(false);
                        break;
                    }
                    catch (TimeoutException) when (timeout == Timeout.Infinite || (elapsed = unchecked(Environment.TickCount - startTime)) < timeout)
                    {
                    }
                }

                Connection.Set();
                Connected?.InvokeAsync(this);
                StartReading();
            }
            finally
            {
                _connectSemaphore.Release();
            }
        }
        
        public async Task<Subscription> ConnectAndSubscribe(int timeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            await Connect(timeout, cancellationToken).ConfigureAwait(false);
            return subscription;
        }

        public event EventHandler Connected;
    }
}