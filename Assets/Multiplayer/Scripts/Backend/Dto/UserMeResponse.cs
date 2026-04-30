using System;

namespace Murang.Multiplayer.Backend.Dto
{
    [Serializable]
    public sealed class UserMeResponse
    {
        public long userId;
        public string metaAccountId;
        public string nickname;
    }
}
