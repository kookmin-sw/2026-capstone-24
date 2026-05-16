using System;

namespace Murang.Multiplayer.Backend.Dto
{
    [Serializable]
    public sealed class ApiErrorResponse
    {
        public string type;
        public string title;
        public int status;
        public string detail;
        public string code;
        public string path;
        public string timestamp;
    }
}
