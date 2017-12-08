using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PSNamedPipe
{
    public class NamedPipeClient : NamedPipe
    {
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

        public void Connect(int timeout = Timeout.Infinite)
        {
            InternalConnect(timeout);
        }

        public async void ConnectAsync(int timeout = Timeout.Infinite)
        {
            await Task.Run(() => InternalConnect(timeout));
        }

        public Subscription ConnectAndSubscribe(int timeout = Timeout.Infinite)
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            Connect(timeout);
            return subscription;
        }

        public Subscription ConnectAsyncAndsubscribe(int timeout = Timeout.Infinite)
        {
            // Prevent race condition by subscribing first
            var subscription = Subscribe();
            ConnectAsync(timeout);
            return subscription;
        }

        protected void InternalConnect(int timeout = Timeout.Infinite)
        {
            _pipe.Connect(timeout);
            _pipe.ReadMode = PipeTransmissionMode.Message;
            Connection.Set();
            BeginRead();
            
            // All other events fire on a separate thread (inside an AsyncCallback),
            // So let's ensure the same is true about this one
            Connected?.BeginInvoke(this, EventArgs.Empty, x => ((EventHandler) x.AsyncState).EndInvoke(x), Connected);
        }
        
        public event EventHandler Connected;
    }
}