using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Song Row UI")]
    public class SongRowUI : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TextMeshProUGUI titleLabel;
        [SerializeField] TextMeshProUGUI artistLabel;

        ISongEntry _song;
        Action<ISongEntry> _callback;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        public void Setup(ISongEntry song, bool active, Action<ISongEntry> callback)
        {
            _song     = song;
            _callback = callback;

            // null-safe lazy fetch: SerializedField wiring 누락 케이스 fall-back
            if (button == null)
            {
                button = GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(OnClick);
            }

            if (titleLabel  != null) titleLabel.text  = song.Title;
            if (artistLabel != null) artistLabel.text = song.Artist;

            if (button != null)
            {
                button.interactable = active;
                float a = active ? 1f : 0.4f;
                if (titleLabel  != null) { var c = titleLabel.color;  c.a = a; titleLabel.color  = c; }
                if (artistLabel != null) { var c = artistLabel.color; c.a = a; artistLabel.color = c; }
            }
        }

        public void OnClick()
        {
            _callback?.Invoke(_song);
        }
    }
}
