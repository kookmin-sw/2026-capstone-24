using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Toggle Button UI")]
    public class InstrumentToggleButtonUI : MonoBehaviour
    {
        [SerializeField] Toggle toggle;
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] Image backgroundImage;

        string _instrumentId;
        Action<string, bool> _callback;

        static readonly Color OnColor  = new Color(0.290f, 0.620f, 1.000f, 0.85f);
        static readonly Color OffColor = new Color(1f, 1f, 1f, 0.15f);

        public void Setup(string instrumentId, bool defaultOn, Action<string, bool> callback)
        {
            _instrumentId = instrumentId;
            _callback     = callback;
            if (label  != null) label.text = instrumentId;
            if (toggle != null)
            {
                toggle.isOn = defaultOn;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(OnToggleChanged);
            }
            UpdateVisual(defaultOn);
        }

        void OnToggleChanged(bool isOn)
        {
            UpdateVisual(isOn);
            _callback?.Invoke(_instrumentId, isOn);
        }

        void UpdateVisual(bool isOn)
        {
            if (backgroundImage != null)
                backgroundImage.color = isOn ? OnColor : OffColor;
        }
    }
}
