using System;

namespace Murang.Multiplayer.Auth
{
    public sealed class AuthFailedException : Exception
    {
        public string ApiCode { get; }

        public AuthFailedException(string message)
            : base(message)
        {
        }

        public AuthFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public AuthFailedException(string message, string apiCode)
            : base(message)
        {
            ApiCode = apiCode;
        }

        public AuthFailedException(string message, string apiCode, Exception innerException)
            : base(message, innerException)
        {
            ApiCode = apiCode;
        }
    }
}
