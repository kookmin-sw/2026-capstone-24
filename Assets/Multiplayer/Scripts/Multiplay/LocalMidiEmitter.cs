using System.Collections.Generic;
using Fusion;
using Instruments;
using UnityEngine;

namespace Murang.Multiplayer.Multiplay
{
    /// <summary>
    /// TestSceneSanyo 씬의 별도 GameObject 에 부착.
    /// 씬의 모든 InstrumentBase 의 MidiTriggered 이벤트를 구독하고,
    /// 로컬 플레이어가 친 MIDI 이벤트를 MidiNetBus RPC 를 통해 룸 전체에 브로드캐스트한다.
    /// client-only 가드: server build (IsServer=true) 에서는 RPC 송신 skip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalMidiEmitter : MonoBehaviour
    {
        private readonly List<InstrumentBase> _subscribedInstruments = new List<InstrumentBase>();
        private NetworkRunner _runner;
        private MidiNetBus _cachedBus;

        private void Start()
        {
            // 씬의 모든 InstrumentBase 를 발견해 MidiTriggered 구독
            InstrumentBase[] all = Object.FindObjectsByType<InstrumentBase>(FindObjectsSortMode.None);
            foreach (InstrumentBase inst in all)
            {
                inst.MidiTriggered += OnLocalMidi;
                _subscribedInstruments.Add(inst);
            }

            Debug.Log($"[LocalMidiEmitter] Subscribed to {_subscribedInstruments.Count} InstrumentBase instance(s)");
        }

        private void OnLocalMidi(MidiEvent e)
        {
            // NetworkRunner 조회 (lazy)
            if (_runner == null)
                _runner = Object.FindFirstObjectByType<NetworkRunner>();

            if (_runner == null || !_runner.IsRunning)
            {
                Debug.Log("[LocalMidiEmitter] NetworkRunner not running, skipping send");
                return;
            }

            // server-only build 에서는 RPC 송신 skip
            if (_runner.IsServer)
                return;

            // MidiNetBus 조회 (lazy cache, null 이면 재시도)
            if (_cachedBus == null)
                _cachedBus = Object.FindFirstObjectByType<MidiNetBus>();

            if (_cachedBus == null)
            {
                Debug.Log("[LocalMidiEmitter] MidiNetBus not found, skipping send");
                return;
            }

            // InstrumentId 확보: MidiEvent 에 이미 채워져 있으면 그대로, 0 이면 skip (InstrumentBase.TriggerMidi 가 자동 채움)
            ushort instId = e.InstrumentId;
            if (instId == 0)
            {
                Debug.Log($"[LocalMidiEmitter] InstrumentId=0 on event note={e.Note}, skipping send (instrumentNetId not set?)");
                return;
            }

            Debug.Log($"[LocalMidiEmitter] Sending Rpc_BroadcastMidi instrumentId={instId} note={e.Note} velocity={e.Velocity} type={e.Type}");
            _cachedBus.Rpc_BroadcastMidi(instId, e.Note, e.Velocity, (byte)e.Type, e.Channel);
        }

        private void OnDestroy()
        {
            foreach (InstrumentBase inst in _subscribedInstruments)
            {
                if (inst != null)
                    inst.MidiTriggered -= OnLocalMidi;
            }

            _subscribedInstruments.Clear();
        }
    }
}
