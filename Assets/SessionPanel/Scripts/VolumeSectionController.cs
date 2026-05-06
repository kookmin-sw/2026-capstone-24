using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Instruments;

namespace SessionPanel
{
    public class VolumeSectionController : MonoBehaviour
    {
        [SerializeField] private GameObject volumeSliderPrefab;
        [SerializeField] private Transform sliderParent;
        [SerializeField] private UnityEngine.Object activeInstrumentProviderObject;

        private IActiveInstrumentProvider _provider;
        private IActiveInstrument _currentInstrument;

        private Slider _masterSlider;
        private TMP_Text _masterLabel;

        private GameObject _instanceSliderGO;
        private Slider _instanceSlider;
        private TMP_Text _instanceLabel;

        private void Awake()
        {
            _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;

            Transform masterParent = sliderParent != null ? sliderParent : transform;
            GameObject masterGO = Instantiate(volumeSliderPrefab, masterParent);
            _masterSlider = masterGO.GetComponentInChildren<Slider>(true);
            _masterLabel = masterGO.GetComponentInChildren<TMP_Text>(true);

            if (_masterLabel != null)
                _masterLabel.text = $"Master\n{Mathf.RoundToInt(SessionVolume.Master * 100)}%";

            if (_masterSlider != null)
            {
                _masterSlider.minValue = 0f;
                _masterSlider.maxValue = 1f;
                _masterSlider.wholeNumbers = false;
                _masterSlider.value = SessionVolume.Master;
                _masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            }
        }

        private void OnEnable()
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
        }

        private void OnDisable()
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
        }

        /// <summary>
        /// SessionPanelController가 prefab 인스턴스화 직후 provider를 주입하기 위해 호출합니다.
        /// Awake 이후에도 안전하게 provider를 교체합니다.
        /// </summary>
        public void InjectProvider(UnityEngine.Object providerObj)
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;

            activeInstrumentProviderObject = providerObj;
            _provider = providerObj as IActiveInstrumentProvider;

            if (_provider != null)
            {
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
                OnActiveInstrumentChanged(_provider.Current);
            }
        }

        private void OnMasterSliderChanged(float value)
        {
            SessionVolume.Master = value;
            if (_masterLabel != null)
                _masterLabel.text = $"Master\n{Mathf.RoundToInt(value * 100)}%";
        }

        private void OnActiveInstrumentChanged(IActiveInstrument instrument)
        {
            _currentInstrument = instrument;

            if (instrument == null)
            {
                if (_instanceSliderGO != null)
                    _instanceSliderGO.SetActive(false);
                return;
            }

            if (_instanceSliderGO == null)
            {
                Transform parent = sliderParent != null ? sliderParent : transform;
                _instanceSliderGO = Instantiate(volumeSliderPrefab, parent);
                _instanceSlider = _instanceSliderGO.GetComponentInChildren<Slider>(true);
                _instanceLabel = _instanceSliderGO.GetComponentInChildren<TMP_Text>(true);

                if (_instanceSlider != null)
                {
                    _instanceSlider.minValue = 0f;
                    _instanceSlider.maxValue = 1f;
                    _instanceSlider.wholeNumbers = false;
                    _instanceSlider.onValueChanged.AddListener(OnInstanceSliderChanged);
                }
            }

            _instanceSliderGO.SetActive(true);

            if (_instanceLabel != null)
                _instanceLabel.text = $"{instrument.InstrumentId}\n{Mathf.RoundToInt(SessionVolume.LoadInstance(instrument.InstrumentId, 0.5f) * 100)}%";

            if (_instanceSlider != null)
            {
                _instanceSlider.onValueChanged.RemoveListener(OnInstanceSliderChanged);
                _instanceSlider.value = SessionVolume.LoadInstance(instrument.InstrumentId, 0.5f);
                _instanceSlider.onValueChanged.AddListener(OnInstanceSliderChanged);
            }
        }

        private void OnInstanceSliderChanged(float value)
        {
            if (_currentInstrument == null)
                return;

            if (_currentInstrument is InstrumentBase instrumentBase)
                instrumentBase.InstanceVolume = value;
            else
                SessionVolume.PersistInstance(_currentInstrument.InstrumentId, value);

            if (_instanceLabel != null)
                _instanceLabel.text = $"{_currentInstrument.InstrumentId}\n{Mathf.RoundToInt(value * 100)}%";
        }
    }
}
