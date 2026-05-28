using System;
using System.Collections.Generic;
using UnityEngine;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// <see cref="IRoomActionSink"/> nop 구현 MonoBehaviour. Inspector에서 seedRows를
    /// 등록하고 emitOnEnable=true로 두면 OnEnable 시 RoomListUpdated를 즉시 발화해
    /// UI 구조 검증과 수동 테스트를 지원한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StubRoomActionSink : MonoBehaviour, IRoomActionSink
    {
        [Header("Seed Data")]
        [SerializeField] private LobbyRoomRowData[] seedRows = Array.Empty<LobbyRoomRowData>();
        [SerializeField] private bool emitOnEnable;

        [Header("Debug — Last Payloads (read-only)")]
        [SerializeField] private string lastCreatePayload;
        [SerializeField] private string lastJoinPayload;

        public event Action<IReadOnlyList<LobbyRoomRowData>> RoomListUpdated;

        private void OnEnable()
        {
            if (emitOnEnable)
            {
                RoomListUpdated?.Invoke(seedRows);
            }
        }

        public void RequestCreate(LobbyCreateRequest request)
        {
            lastCreatePayload =
                $"RoomName={request.RoomName} MaxPlayers={request.MaxPlayers} " +
                $"PasswordEnabled={request.PasswordEnabled} RuntimeVersion={request.RuntimeVersion}";
        }

        public void RequestJoin(LobbyJoinRequest request)
        {
            lastJoinPayload =
                $"RoomId={request.RoomId} RoomName={request.RoomName} " +
                $"PasswordRaw={request.PasswordRaw} RuntimeVersion={request.RuntimeVersion}";
        }
    }
}
