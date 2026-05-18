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

        string _instrumentId;
        Action<string, bool> _callback;

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
        }

        void OnToggleChanged(bool isOn)
        {
            _callback?.Invoke(_instrumentId, isOn);
        }
    }
}
