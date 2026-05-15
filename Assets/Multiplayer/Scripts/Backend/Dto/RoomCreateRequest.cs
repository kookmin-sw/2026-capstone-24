using System;

namespace Murang.Multiplayer.Backend.Dto
{
    /// <summary>
    /// Client → Spring <c>POST /api/v1/rooms</c> request body.
    ///
    /// <para><c>photonSessionName</c> 은 영문/숫자/_/- 문자만 허용되며 128자 이내.
    /// <c>passwordHash</c> 는 <see cref="Murang.Multiplayer.Room.Common.RoomPasswordHasher"/>
    /// 로 만든 SHA256-base64 문자열 또는 null/빈 문자열 (비밀번호 없음).</para>
    /// </summary>
    [Serializable]
    public sealed class RoomCreateRequest
    {
        public string photonSessionName;
        public int maxPlayers;
        public string passwordHash;
        public string roomRuntimeVersion;
    }
}
