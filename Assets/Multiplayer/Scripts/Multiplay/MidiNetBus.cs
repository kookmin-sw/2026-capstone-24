using System.Collections.Generic;
using Fusion;
using Instruments;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// NetworkObject spawned once per room for MIDI broadcast.
    /// Fusion 2 dedicated-server 토폴로지에서는 client → other client 직접 RPC 가 동작하지 않으므로
    /// 2단계 relay 패턴을 사용한다:
    ///   1) client → server : RPC_SendMidiToServer (RpcSources.All, RpcTargets.StateAuthority)
    ///   2) server → all clients : RPC_RelayMidiToClients (RpcSources.StateAuthority, RpcTargets.All)
    /// </summary>
    public sealed class MidiNetBus : NetworkBehaviour
    {
        // Server-side sustained note tracking for cleanup on player leave.
        private readonly Dictionary<PlayerRef, List<(ushort instId, int note)>> _sustainedNotes =
            new Dictionary<PlayerRef, List<(ushort, int)>>();

        /// <summary>
        /// Client → server. LocalMidiEmitter 가 자기 MidiTriggered 를 받아 호출한다.
        /// server 는 sustained 트래킹 후 RPC_RelayMidiToClients 로 룸 전체에 relay.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SendMidiToServer(
            ushort instrumentId,
            int note,
            float velocity,
            byte type,
            byte channel,
            RpcInfo info = default)
        {
            MidiEventType eventType = (MidiEventType)type;

            // ControlChange is not synchronized yet.
            if (eventType == MidiEventType.ControlChange)
            {
                Debug.Log($"[MidiNetBus] RPC_SendMidiToServer: ControlChange skipped (instrumentId={instrumentId})");
                return;
            }

            Debug.Log($"[MidiNetBus] RPC_SendMidiToServer server recv: source={info.Source} instrumentId={instrumentId} note={note} velocity={velocity} type={eventType}");

            TrackSustainedNote(info.Source, instrumentId, note, eventType);

            // server → all clients relay (sender 정보를 payload 로 동봉해 echo 가드용)
            RPC_RelayMidiToClients(instrumentId, note, velocity, type, channel, info.Source);
        }

        /// <summary>
        /// Server → all clients relay. 자기 자신이 보낸 이벤트는 sourcePlayer 비교로 echo 가드.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_RelayMidiToClients(
            ushort instrumentId,
            int note,
            float velocity,
            byte type,
            byte channel,
            PlayerRef sourcePlayer)
        {
            // server 본인은 relay 단계에서 audio dispatch 안 함 (SendMidiToServer 에서 이미 처리)
            if (Runner.IsServer)
                return;

            MidiEventType eventType = (MidiEventType)type;

            // client-side: 자기 발신 echo 가드
            if (sourcePlayer == Runner.LocalPlayer)
            {
                Debug.Log($"[MidiNetBus] RPC_RelayMidiToClients echo-guarded: source={sourcePlayer} instrumentId={instrumentId} note={note}");
                return;
            }

            Debug.Log($"[MidiNetBus] RPC_RelayMidiToClients received: source={sourcePlayer} instrumentId={instrumentId} note={note} velocity={velocity} type={eventType}");

            if (!InstrumentIdRegistry.TryResolve(instrumentId, out InstrumentBase inst))
            {
                Debug.Log($"[MidiNetBus] InstrumentBase not found for instrumentId={instrumentId}, skipping");
                return;
            }

            inst.ApplyRemoteMidi(new MidiEvent(note, velocity, eventType, channel, instrumentId));
        }

        /// <summary>
        /// Flush sustained notes for a player who left the room.
        /// server 에서 직접 RPC_RelayMidiToClients(NoteOff) 발행 — 1단계로 충분.
        /// </summary>
        public void FlushSustainedNotesForPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (!_sustainedNotes.TryGetValue(player, out var notes) || notes.Count == 0)
            {
                _sustainedNotes.Remove(player);
                return;
            }

            Debug.Log($"[MidiNetBus] OnPlayerLeft flush: player={player} sustainedCount={notes.Count}");

            foreach ((ushort instId, int note) in notes)
            {
                RPC_RelayMidiToClients(instId, note, 0f, (byte)MidiEventType.NoteOff, 0, player);
            }

            _sustainedNotes.Remove(player);
        }

        private void TrackSustainedNote(PlayerRef player, ushort instId, int note, MidiEventType eventType)
        {
            if (!_sustainedNotes.TryGetValue(player, out var list))
            {
                list = new List<(ushort, int)>();
                _sustainedNotes[player] = list;
            }

            if (eventType == MidiEventType.NoteOn)
            {
                list.Add((instId, note));
            }
            else if (eventType == MidiEventType.NoteOff || eventType == MidiEventType.Choke)
            {
                list.RemoveAll(entry => entry.instId == instId && entry.note == note);
            }
        }
    }
}
