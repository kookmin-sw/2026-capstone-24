using System;

namespace Murang.Multiplayer.Auth
{
    public sealed class AuthFailedException : Exception
    {
        public AuthFailedException(string message)
            : base(message)
        {
        }

        public AuthFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
