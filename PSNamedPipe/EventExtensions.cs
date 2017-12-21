using System;
using System.Threading.Tasks;

namespace PSNamedPipe
{
    public static class EventExtensions
    {
        public static async void InvokeAsync(this EventHandler handler, object sender)
        {
            await Task.Run(() => handler?.Invoke(sender, EventArgs.Empty)).ConfigureAwait(false);
        }

        public static async void InvokeAsync<T>(this EventHandler<T> handler, object sender, T args) where T : EventArgs
        {
            await Task.Run(() => handler?.Invoke(sender, args)).ConfigureAwait(false);
        }
    }
}