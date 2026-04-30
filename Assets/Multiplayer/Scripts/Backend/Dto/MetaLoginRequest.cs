using System;

namespace Murang.Multiplayer.Backend.Dto
{
    [Serializable]
    public sealed class MetaLoginRequest
    {
        public string metaIdToken;
        public string nickname;
    }
}
