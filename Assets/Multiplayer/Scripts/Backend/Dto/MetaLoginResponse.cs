using System;

namespace Murang.Multiplayer.Backend.Dto
{
    [Serializable]
    public sealed class MetaLoginResponse
    {
        public string accessToken;
        public string refreshToken;
        public UserSummary user;

        [Serializable]
        public sealed class UserSummary
        {
            public long userId;
            public string nickname;
        }
    }
}
