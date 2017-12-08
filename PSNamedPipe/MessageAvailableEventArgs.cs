using System;

namespace PSNamedPipe
{
    public class MessageAvailableEventArgs : EventArgs
    {
        public MessageAvailableEventArgs()
        {
        }

        public MessageAvailableEventArgs(byte[] message)
        {
            Message = message;
        }
        
        public byte[] Message { get; }
    }
}