using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 텔레포트 앵커 기반 IActiveInstrumentProvider 구현체.
    /// InstrumentTeleportLink의 AnyAnchorTeleported 이벤트를 구독해
    /// 텔레포트 목적지의 연결 악기로 Current를 자동 전환한다.
    /// 비악기 구역(null 연결)으로 텔레포트하면 Current = null(None).
    /// </summary>
    [AddComponentMenu("Instruments/Teleport Instrument Provider")]
    public class TeleportInstrumentProvider : MonoBehaviour, IActiveInstrumentProvider
    {
        public IActiveInstrument Current => _current;
        public event System.Action<IActiveInstrument> ActiveInstrumentChanged;

        IActiveInstrument _current;

        void OnEnable()
        {
            InstrumentTeleportLink.AnyAnchorTeleported += OnAnchorTeleported;
        }

        void OnDisable()
        {
            InstrumentTeleportLink.AnyAnchorTeleported -= OnAnchorTeleported;
        }

        void OnAnchorTeleported(InstrumentBase instrument)
        {
            // InstrumentBase : IActiveInstrument — null이면 None 상태
            IActiveInstrument next = instrument;
            if (ReferenceEquals(_current, next)) return;

            (_current as InstrumentBase)?.SilenceAll();
            _current = next;
            ActiveInstrumentChanged?.Invoke(_current);
        }
    }
}
