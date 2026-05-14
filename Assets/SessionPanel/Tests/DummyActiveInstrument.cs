using UnityEngine;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/[DEBUG] Dummy Active Instrument")]
    public class DummyActiveInstrument : MonoBehaviour, IActiveInstrument
    {
        [SerializeField] private Transform _panelAnchor;
        [SerializeField] private string _instrumentId = "dummy";
        [SerializeField] private Transform _instrumentRoot;
        [SerializeField, Range(0f, 1f)] private float _instanceVolume = 0.5f;

        public Transform PanelAnchor    => _panelAnchor;
        public string InstrumentId      => _instrumentId;
        public Transform InstrumentRoot => _instrumentRoot != null ? _instrumentRoot : transform;

        public float InstanceVolume
        {
            get => _instanceVolume;
            set => _instanceVolume = Mathf.Clamp01(value);
        }
    }
}
