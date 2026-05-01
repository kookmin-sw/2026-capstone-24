using System;

namespace Murang.Multiplayer.Backend.Dto
{
    [Serializable]
    public sealed class UserMeResponse
    {
        public string playerId;
        public string metaAccountId;
        public string nickname;
    }
}
