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
        [SerializeField] Image backgroundImage;

        int _channel;
        Action<int, bool> _callback;

        static readonly Color OnColor  = new Color(0.290f, 0.620f, 1.000f, 0.85f);
        static readonly Color OffColor = new Color(1f, 1f, 1f, 0.15f);

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
            UpdateVisual(defaultOn);
        }

        void OnToggleChanged(bool isOn)
        {
            UpdateVisual(isOn);
            _callback?.Invoke(_channel, isOn);
        }

        void UpdateVisual(bool isOn)
        {
            if (backgroundImage != null)
                backgroundImage.color = isOn ? OnColor : OffColor;
        }
    }
}
