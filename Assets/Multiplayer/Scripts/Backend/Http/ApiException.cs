using System;

namespace Murang.Multiplayer.Backend.Http
{
    public sealed class ApiException : Exception
    {
        public ApiException(long statusCode, string code, string detail, string responseBody)
            : base(string.IsNullOrEmpty(detail) ? code : detail)
        {
            StatusCode = statusCode;
            Code = code;
            Detail = detail;
            ResponseBody = responseBody;
        }

        public long StatusCode { get; }

        public string Code { get; }

        public string Detail { get; }

        public string ResponseBody { get; }
    }
}
