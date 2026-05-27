using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Instruments;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Instrument Guide Panel Controller")]
    public class InstrumentGuidePanelController : MonoBehaviour
    {
        [SerializeField] Image pageImage;
        [SerializeField] TextMeshProUGUI descriptionText;
        [SerializeField] TextMeshProUGUI titleLabel;
        [SerializeField] TextMeshProUGUI pageIndicator;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] Button closeButton;
        [SerializeField] string placeholderMessage = "Content not available.";

        readonly List<Page> _pages = new List<Page>();
        int _pageIndex;

        struct Page
        {
            public Sprite image;
            public string description;
        }

        void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(OnPrev);
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>InstrumentTravelSectionController.OnGuideRequested 가 호출.</summary>
        public void Open(InstrumentBase instrument)
        {
            if (instrument == null) return;
            LoadPages(instrument.InstrumentId);
            _pageIndex = 0;
            gameObject.SetActive(true);
            if (titleLabel != null)
                titleLabel.text = instrument.InstrumentId;
            Apply();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void LoadPages(string instrumentId)
        {
            _pages.Clear();
            if (string.IsNullOrEmpty(instrumentId)) return;

            // 폴더 규약: Resources/Tutorial/<instrumentId>/<NN>/{page, description}
            // NN 은 01 부터 순차 — 둘 다 null 이면 스캔 종료.
            // Sprite 임포트 타입 없이도 동작하도록 Texture2D → Sprite.Create 경로 사용.
            int n = 1;
            while (true)
            {
                string pad = n.ToString("00");
                string folder = $"Tutorial/{instrumentId}/{pad}";
                var tex = Resources.Load<Texture2D>($"{folder}/page");
                var textAsset = Resources.Load<TextAsset>($"{folder}/description");
                if (tex == null && textAsset == null) break;
                Sprite sprite = null;
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _pages.Add(new Page
                {
                    image = sprite,
                    description = textAsset != null ? textAsset.text : string.Empty,
                });
                n++;
                if (n > 99) break; // 안전 한도
            }
        }

        void OnPrev()
        {
            if (_pages.Count == 0 || _pageIndex <= 0) return;
            _pageIndex--;
            Apply();
        }

        void OnNext()
        {
            if (_pages.Count == 0 || _pageIndex >= _pages.Count - 1) return;
            _pageIndex++;
            Apply();
        }

        void Apply()
        {
            bool hasPages = _pages.Count > 0;

            if (hasPages)
            {
                var page = _pages[_pageIndex];
                if (pageImage != null)
                {
                    pageImage.sprite = page.image;
                    pageImage.enabled = page.image != null;
                }
                if (descriptionText != null)
                    descriptionText.text = page.description;
                if (pageIndicator != null)
                    pageIndicator.text = $"{_pageIndex + 1} / {_pages.Count}";
            }
            else
            {
                // 콘텐츠 없음 — graceful fallback.
                if (pageImage != null) pageImage.enabled = false;
                if (descriptionText != null) descriptionText.text = placeholderMessage;
                if (pageIndicator != null) pageIndicator.text = "0 / 0";
            }

            // 첫/마지막 페이지에서 각 버튼 비활성.
            if (prevButton != null)
                prevButton.interactable = hasPages && _pageIndex > 0;
            if (nextButton != null)
                nextButton.interactable = hasPages && _pageIndex < _pages.Count - 1;
        }
    }
}
