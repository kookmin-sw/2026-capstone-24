using System.Collections.Generic;
using Fusion;
using Instruments;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// 룸당 1개 spawn 되는 NetworkObject.
    /// 클라이언트가 친 MIDI 이벤트를 룸 전체에 RPC 브로드캐스트하고,
    /// 수신 측은 InstrumentIdRegistry 로 해당 InstrumentBase 를 조회해 ApplyRemoteMidi 를 호출한다.
    /// server 측은 오디오 dispatch 없이 sustained note 트래킹만 담당한다.
    /// </summary>
    public sealed class MidiNetBus : NetworkBehaviour
    {
        // server-side: 플레이어별 sustained NoteOn 트래킹 (NoteOff/Choke 가 없을 때 PlayerLeft 시 flush)
        private readonly Dictionary<PlayerRef, List<(ushort instId, int note)>> _sustainedNotes =
            new Dictionary<PlayerRef, List<(ushort, int)>>();

        /// <summary>
        /// 모든 클라이언트에 MIDI 이벤트를 브로드캐스트한다.
        /// LocalMidiEmitter 가 클라이언트 측에서 자기 MidiTriggered 를 받아 호출한다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_BroadcastMidi(
            ushort instrumentId,
            int note,
            float velocity,
            byte type,
            byte channel,
            RpcInfo info = default)
        {
            MidiEventType eventType = (MidiEventType)type;

            // ControlChange (값 3) 는 본 단계 미사용 — silent skip
            if (eventType == MidiEventType.ControlChange)
            {
                Debug.Log($"[MidiNetBus] Rpc_BroadcastMidi: ControlChange skipped (instrumentId={instrumentId})");
                return;
            }

            // server-side: sustained note 트래킹 (오디오 dispatch 없음)
            if (Runner.IsServer)
            {
                TrackSustainedNote(info.Source, instrumentId, note, eventType);
                return;
            }

            // client-side: 자기 발신 echo 가드
            if (info.Source == Runner.LocalPlayer)
            {
                Debug.Log($"[MidiNetBus] echo-guarded self RPC from player={info.Source} instrumentId={instrumentId} note={note}");
                return;
            }

            Debug.Log($"[MidiNetBus] Rpc_BroadcastMidi received: player={info.Source} instrumentId={instrumentId} note={note} velocity={velocity} type={eventType}");

            // InstrumentIdRegistry 로 로컬 InstrumentBase 조회 후 ApplyRemoteMidi
            if (!InstrumentIdRegistry.TryResolve(instrumentId, out InstrumentBase inst))
            {
                Debug.Log($"[MidiNetBus] InstrumentBase not found for instrumentId={instrumentId}, skipping");
                return;
            }

            inst.ApplyRemoteMidi(new MidiEvent(note, velocity, eventType, channel, instrumentId));
        }

        /// <summary>
        /// 플레이어가 룸을 떠날 때 호출된다. (MidiNetBusSpawner 의 OnPlayerLeft 에서 트리거)
        /// server-side sustained note flush — NoteOff 를 자동 발행해 다른 클라이언트의 발음을 정지.
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
                Rpc_BroadcastMidi(instId, note, 0f, (byte)MidiEventType.NoteOff, 0);
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
