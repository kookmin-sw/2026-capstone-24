using System;

namespace Murang.Multiplayer.Backend.Dto
{
    /// <summary>
    /// Spring → Client 룸 응답. <c>POST /api/v1/rooms</c> 생성 응답과
    /// <c>GET /api/v1/rooms/{id}</c> 조회 응답이 같은 모양을 공유한다.
    ///
    /// <para>ready callback 도달 전에는 <c>taskPublicIp</c>/<c>gamePort</c>/<c>readyAt</c>
    /// 가 null 이며 <c>status</c> 는 보통 <c>PROVISIONING</c> 또는 <c>SERVER_STARTING</c>.
    /// 클라이언트는 <c>status == READY</c> 가 될 때까지 GET 으로 폴링한다.</para>
    /// </summary>
    [Serializable]
    public sealed class RoomResponse
    {
        public long roomId;
        public string photonSessionName;
        public int maxPlayers;
        public bool locked;
        public string status;
        public string taskPublicIp;
        public int gamePort;
        public string createdAt;
        public string readyAt;

        public bool IsReady => string.Equals(status, "READY", StringComparison.Ordinal);

        public bool IsTerminal =>
            string.Equals(status, "TERMINATED", StringComparison.Ordinal)
            || string.Equals(status, "FAILED", StringComparison.Ordinal)
            || string.Equals(status, "UNHEALTHY", StringComparison.Ordinal)
            || string.Equals(status, "TERMINATING", StringComparison.Ordinal);
    }
}
