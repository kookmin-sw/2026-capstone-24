using System.Collections.Generic;
using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 씬 로드 시 ushort InstrumentNetId 와 InstrumentBase 인스턴스를 매핑하는 process-global registry.
    /// InstrumentBase.OnEnable / OnDisable 에서 자동 Register / Unregister 된다.
    /// </summary>
    public static class InstrumentIdRegistry
    {
        private static readonly Dictionary<ushort, InstrumentBase> _map =
            new Dictionary<ushort, InstrumentBase>();

        /// <summary>
        /// InstrumentBase 를 registry 에 등록한다.
        /// InstrumentNetId == 0 이면 네트워크 동기화 비활성으로 간주하고 skip.
        /// 같은 ushort 가 이미 등록된 경우 reject.
        /// </summary>
        public static void Register(InstrumentBase inst)
        {
            if (inst == null)
                return;

            ushort id = inst.InstrumentNetId;

            if (id == 0)
            {
                Debug.LogWarning(
                    $"[InstrumentIdRegistry] skip {inst.name}: InstrumentNetId=0 (not synced)");
                return;
            }

            if (_map.TryGetValue(id, out InstrumentBase existing))
            {
                if (existing == inst)
                    return; // 동일 인스턴스 중복 호출 — 무시
                Debug.LogError(
                    $"[InstrumentIdRegistry] duplicate {id} reject {inst.name} (existing={existing.name})");
                return;
            }

            _map[id] = inst;
            Debug.Log($"[InstrumentIdRegistry] Registered id={id} name={inst.name}");
        }

        /// <summary>InstrumentBase 를 registry 에서 제거한다.</summary>
        public static void Unregister(InstrumentBase inst)
        {
            if (inst == null)
                return;

            ushort id = inst.InstrumentNetId;
            if (id == 0)
                return;

            if (_map.TryGetValue(id, out InstrumentBase existing) && existing == inst)
            {
                _map.Remove(id);
                Debug.Log($"[InstrumentIdRegistry] Unregistered id={id} name={inst.name}");
            }
        }

        /// <summary>ushort id 로 InstrumentBase 를 조회한다.</summary>
        public static bool TryResolve(ushort id, out InstrumentBase inst)
        {
            return _map.TryGetValue(id, out inst);
        }
    }
}
