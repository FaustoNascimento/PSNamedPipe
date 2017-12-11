using System;
using System.IO.Pipes;
using System.Threading;

namespace PSNamedPipe
{
    public class NamedPipeClient : NamedPipe
    {
        private readonly NamedPipeClientStream _pipe;
        private CancellationTokenSource _connectCancellationTokenSource;

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

        public void Connect()
        {
            Connect(new CancellationToken());
        }

        public async void Connect(CancellationToken cancellationToken)
        {
            _connectCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await _pipe.ConnectAsync(cancellationToken);

            if (_pipe.IsConnected)
            {
                _connectCancellationTokenSource.Dispose();
                _pipe.ReadMode = PipeTransmissionMode.Message;
                Connection.Set();
                Connected?.BeginInvoke(this, EventArgs.Empty, x => ((EventHandler)x.AsyncState).EndInvoke(x), Connected);

                BeginRead();
            }
        }

        public Subscription ConnectAndSubscribe()
        {
            return ConnectAndSubscribe(new CancellationToken());
        }

        public Subscription ConnectAndSubscribe(CancellationToken cancellationToken)
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            Connect(cancellationToken);
            return subscription;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                _connectCancellationTokenSource?.Cancel();
                _connectCancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException)
            {

            }            

            base.Dispose(disposing);
        }

        public event EventHandler Connected;
    }
}