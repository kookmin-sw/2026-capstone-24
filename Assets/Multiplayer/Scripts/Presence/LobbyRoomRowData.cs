using System;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 방 목록 행 UI 전용 POCO. backend <c>RoomListEntry</c> DTO와 분리되어
    /// <see cref="IRoomActionSink"/> 이벤트를 통해 전달된다.
    /// </summary>
    [Serializable]
    public sealed class LobbyRoomRowData
    {
        public string RoomId;
        public string RoomName;
        public int CurrentPlayers;
        public int MaxPlayers;
        public bool HasPassword;
    }
}
