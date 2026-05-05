using UnityEngine;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/[DEBUG] Dummy Active Instrument Provider")]
    public class DummyActiveInstrumentProvider : MonoBehaviour, IActiveInstrumentProvider
    {
        public event System.Action<IActiveInstrument> ActiveInstrumentChanged;

        [SerializeField] private DummyActiveInstrument _current;

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
