using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Accompaniment Toggle UI")]
    public class AccompanimentToggleUI : MonoBehaviour
    {
        [SerializeField] Toggle toggle;
        [SerializeField] TextMeshProUGUI label;

        int _channel;
        Action<int, bool> _callback;

        public void Setup(int channel, string instrumentKey, bool defaultOn, Action<int, bool> callback)
        {
            _channel  = channel;
            _callback = callback;
            if (label  != null) label.text = instrumentKey;
            if (toggle != null)
            {
                toggle.isOn = defaultOn;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        void OnToggleChanged(bool isOn)
        {
            _callback?.Invoke(_channel, isOn);
        }
    }
}
