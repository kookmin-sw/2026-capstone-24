using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Travel Item")]
    public class InstrumentTravelItem : MonoBehaviour
    {
        [SerializeField] Image instrumentImage;          // 항목 상단 큰 식별 이미지
        [SerializeField] TextMeshProUGUI instrumentLabel; // 식별 보조 (instrumentId fallback)
        [SerializeField] Button travelButton;            // [이동]
        [SerializeField] Button guideButton;             // [가이드] (plan 2/2에서 동작 부착)

        InstrumentBase _instrument;
        Action<InstrumentTravelItem> _onTravel;
        Action<InstrumentTravelItem> _onGuide;
        bool _isCurrent;

        public InstrumentBase Instrument => _instrument;
        public bool IsCurrent => _isCurrent;

        public void Setup(InstrumentBase instrument,
                          Action<InstrumentTravelItem> onTravel,
                          Action<InstrumentTravelItem> onGuide)
        {
            _instrument = instrument;
            _onTravel = onTravel;
            _onGuide = onGuide;

            if (instrumentLabel != null)
                instrumentLabel.text = instrument != null ? instrument.InstrumentId : "(unknown)";

            if (instrumentImage != null && instrument != null)
            {
                var tex = Resources.Load<Texture2D>($"Thumbnails/{instrument.InstrumentId}");
                if (tex != null)
                {
                    instrumentImage.sprite = Sprite.Create(
                        tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    instrumentImage.enabled = true;
                }
                else
                {
                    instrumentImage.enabled = false;
                }
            }

            if (travelButton != null)
            {
                travelButton.onClick.RemoveAllListeners();
                travelButton.onClick.AddListener(OnTravelClick);
            }

            if (guideButton != null)
            {
                guideButton.onClick.RemoveAllListeners();
                guideButton.onClick.AddListener(OnGuideClick);
            }
        }

        public void SetCurrent(bool isCurrent)
        {
            _isCurrent = isCurrent;
            if (travelButton != null)
                travelButton.interactable = !isCurrent;
        }

        void OnTravelClick()
        {
            if (_isCurrent) return;
            _onTravel?.Invoke(this);
        }

        void OnGuideClick()
        {
            // TODO sub-spec 10 plan 2/2: InstrumentGuidePanel.Open(_instrument.InstrumentId)
            _onGuide?.Invoke(this);
        }
    }
}
