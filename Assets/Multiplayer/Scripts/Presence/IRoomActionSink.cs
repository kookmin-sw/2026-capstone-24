using System;
using System.Collections.Generic;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// 방 생성 요청 데이터. backend DTO와 분리된 UI 전용 struct.
    /// decision 02 admission gate constraint: <see cref="RuntimeVersion"/> 필드로
    /// 실 구현이 <c>Application.version</c>을 backend에 전달한다.
    /// </summary>
    public readonly struct LobbyCreateRequest
    {
        public string RoomName { get; }
        public int MaxPlayers { get; }
        public bool PasswordEnabled { get; }
        public string PasswordRaw { get; } // PasswordEnabled=false 시 null/empty
        public string RuntimeVersion { get; } // decision 02 admission gate constraint

        public LobbyCreateRequest(
            string roomName,
            int maxPlayers,
            bool passwordEnabled,
            string passwordRaw,
            string runtimeVersion)
        {
            RoomName = roomName;
            MaxPlayers = maxPlayers;
            PasswordEnabled = passwordEnabled;
            PasswordRaw = passwordRaw;
            RuntimeVersion = runtimeVersion;
        }
    }

    /// <summary>
    /// 방 입장 요청 데이터. backend DTO와 분리된 UI 전용 struct.
    /// decision 02 admission gate constraint: <see cref="RuntimeVersion"/> 필드로
    /// 실 구현이 <c>Application.version</c>을 backend에 전달한다.
    /// </summary>
    public readonly struct LobbyJoinRequest
    {
        public string RoomId { get; }
        public string RoomName { get; }
        public string PasswordRaw { get; } // 비밀번호 없는 방 = null/empty
        public string RuntimeVersion { get; }

        public LobbyJoinRequest(
            string roomId,
            string roomName,
            string passwordRaw,
            string runtimeVersion)
        {
            RoomId = roomId;
            RoomName = roomName;
            PasswordRaw = passwordRaw;
            RuntimeVersion = runtimeVersion;
        }
    }

    /// <summary>
    /// UI ↔ 백엔드 분리 인터페이스. LobbyPanelController가 이 인터페이스를 통해
    /// 백엔드 호출을 위임하므로 UI 레이어는 실 구현체 타입에 의존하지 않는다.
    /// </summary>
    public interface IRoomActionSink
    {
        void RequestCreate(LobbyCreateRequest request);
        void RequestJoin(LobbyJoinRequest request);
        event Action<IReadOnlyList<LobbyRoomRowData>> RoomListUpdated;
    }
}
