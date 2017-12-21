using System;

namespace PSNamedPipe
{
    public class DisconnectedException : Exception
    {
        public DisconnectedException()
        {
        }

        public DisconnectedException(string message) : base(message)
        {
        }

        public DisconnectedException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}