using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Difficulty Button UI")]
    public class DifficultyButtonUI : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TextMeshProUGUI label;

        string _difficulty;
        Action<string, DifficultyButtonUI> _callback;
        Image _bg;

        static readonly Color NormalColor   = new Color(0.18f, 0.22f, 0.4f,  0.9f);
        static readonly Color SelectedColor = new Color(0.25f, 0.5f,  0.85f, 1.0f);

        void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        public void Setup(string difficulty, Action<string, DifficultyButtonUI> callback, Image bg = null)
        {
            _difficulty = difficulty;
            _callback   = callback;
            _bg         = bg;
            if (label != null) label.text = difficulty;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_bg != null)
                _bg.color = selected ? SelectedColor : NormalColor;
        }

        public void OnClick()
        {
            _callback?.Invoke(_difficulty, this);
        }
    }
}
