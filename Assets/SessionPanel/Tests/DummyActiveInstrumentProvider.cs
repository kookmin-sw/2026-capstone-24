using UnityEngine;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/[DEBUG] Dummy Active Instrument Provider")]
    public class DummyActiveInstrumentProvider : MonoBehaviour, IActiveInstrumentProvider
    {
        public event System.Action<IActiveInstrument> ActiveInstrumentChanged;

        /// <summary>
        /// 씬의 Piano/DrumKit InstrumentBase 인스턴스를 직접 할당합니다.
        /// InstrumentBase가 IActiveInstrument를 구현하므로 캐스팅 없이 사용됩니다.
        /// null이면 악기 없음(마스터 슬라이더만 노출).
        /// </summary>
        [SerializeField] private InstrumentBase _current;

        public IActiveInstrument Current => _current;

        private IActiveInstrument _previousCurrent;

        private void Update()
        {
            IActiveInstrument current = _current;
            if (!ReferenceEquals(current, _previousCurrent))
            {
                _previousCurrent = current;
                ActiveInstrumentChanged?.Invoke(current);
            }
        }
    }
}
