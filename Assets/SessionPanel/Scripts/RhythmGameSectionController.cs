using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Instruments;
using RhythmGame.Data;
using RhythmGame.Runtime;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Rhythm Game Section Controller")]
    public class RhythmGameSectionController : MonoBehaviour
    {
        [Header("Song List")]
        [SerializeField] Transform songListContent;
        [SerializeField] GameObject songRowPrefab;

        [Header("Detail Panel")]
        [SerializeField] GameObject noSongPanel;
        [SerializeField] GameObject songSelectedPanel;
        [SerializeField] Button previewButton;
        [SerializeField] Transform bpmBar;
        [SerializeField] Transform difficultyContainer;
        [SerializeField] GameObject difficultyButtonPrefab;
        [SerializeField] Button playButton;

        [Header("Dependencies")]
        [SerializeField] UnityEngine.Object activeInstrumentProviderObject;
        [SerializeField] UnityEngine.Object songCatalogObject;

        IActiveInstrumentProvider _provider;
        ISongCatalog _catalog;
        IActiveInstrument _currentInstrument;

        ISongEntry _selectedSong;
        string _selectedDifficulty;
        VmSongChart _loadedChart;

        float _baseBpm = 120f;
        int _bpmOffset = 0;
        TextMeshProUGUI _bpmLabel;
        DifficultyButtonUI _selectedDiffBtn;

        RhythmGameHost _activeHost;

        /// <summary>리듬게임 세션이 시작됐을 때 발생. SessionPanelController가 패널 전체를 숨기는 데 사용한다.</summary>
        public event System.Action GameStarted;
        /// <summary>리듬게임 세션이 종료됐을 때 발생. SessionPanelController가 패널 전체를 복원하는 데 사용한다.</summary>
        public event System.Action GameEnded;

        void Awake()
        {
            _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
            _catalog  = songCatalogObject as ISongCatalog;

            if (playButton    != null) playButton.onClick.AddListener(OnPlayButtonClicked);
            if (previewButton != null) previewButton.onClick.AddListener(OnPreviewButtonClicked);

            if (songSelectedPanel != null)
            {
                foreach (Transform child in songSelectedPanel.transform)
                {
                    var le = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 40f;
                }
            }
        }

        void OnEnable()
        {
            // null-safe fallback: Inject 이전에 OnEnable이 발화해도 SerializedField 값으로 시도한다.
            if (_provider == null)
                _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
            if (_catalog == null)
                _catalog = songCatalogObject as ISongCatalog;

            // idempotent 구독: 중복 등록 방지를 위해 항상 -= 후 +=
            if (_provider != null)
            {
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
            }
            _currentInstrument = _provider?.Current;
            RefreshSongList();
        }

        void OnDisable()
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
        }

        void OnActiveInstrumentChanged(IActiveInstrument instrument)
        {
            _currentInstrument = instrument;
            ResetSelection();
            RefreshSongList();
        }

        void RefreshSongList()
        {
            ClearChildren(songListContent);
            ShowDetail(false);
            if (_catalog == null || _currentInstrument == null) return;

            foreach (var song in _catalog.Songs)
            {
                bool supported = song.SupportedInstrumentIds.Contains(_currentInstrument.InstrumentId);
                var go = Instantiate(songRowPrefab, songListContent);
                var rowLE = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 50f;
                if (go.GetComponent<CanvasRenderer>() == null)
                    go.AddComponent<CanvasRenderer>();
                var bg = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                Color normalCol  = supported ? new Color(0.22f, 0.28f, 0.48f, 1f) : new Color(0.12f, 0.15f, 0.25f, 0.5f);
                Color hlCol      = supported ? new Color(0.30f, 0.38f, 0.60f, 1f) : normalCol;
                Color pressedCol = supported ? new Color(0.15f, 0.20f, 0.36f, 1f) : normalCol;
                bg.color = normalCol;
                var rowBtn = go.GetComponent<Button>();
                if (rowBtn != null)
                {
                    rowBtn.targetGraphic = bg;
                    var cols = rowBtn.colors;
                    cols.normalColor      = normalCol;
                    cols.highlightedColor = hlCol;
                    cols.pressedColor     = pressedCol;
                    cols.disabledColor    = new Color(0.12f, 0.15f, 0.25f, 0.5f);
                    cols.colorMultiplier  = 1f;
                    rowBtn.colors = cols;
                }
                var row = go.GetComponent<SongRowUI>();
                if (row != null)
                    row.Setup(song, supported, OnSongRowClicked);
            }
        }

        void OnSongRowClicked(ISongEntry song)
        {
            if (_currentInstrument == null) return;
            if (!song.SupportedInstrumentIds.Contains(_currentInstrument.InstrumentId)) return;

            _selectedSong = song;
            _selectedDifficulty = null;
            _loadedChart = null;
            _bpmOffset = 0;
            _selectedDiffBtn = null;

            ClearChildren(difficultyContainer);
            HideBpmBar();

            DifficultyButtonUI firstBtn = null;
            string firstDiff = null;
            foreach (var diff in song.Difficulties)
            {
                var go = Instantiate(difficultyButtonPrefab, difficultyContainer);
                if (go.GetComponent<CanvasRenderer>() == null)
                    go.AddComponent<CanvasRenderer>();
                var diffImg = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                diffImg.color = new Color(0.18f, 0.22f, 0.4f, 0.9f);
                var diffBtnComp = go.GetComponent<Button>();
                if (diffBtnComp != null) diffBtnComp.targetGraphic = diffImg;
                var diffLE = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                diffLE.preferredHeight = 36f;
                var btn = go.GetComponent<DifficultyButtonUI>();
                if (btn != null)
                {
                    btn.Setup(diff, OnDifficultyClicked, diffImg);
                    if (firstBtn == null) { firstBtn = btn; firstDiff = diff; }
                }
            }

            // 첫 번째 난이도를 자동 선택해 곡 클릭 직후 Play 가능하게 함
            // (LoadChart 내부에서 BPM 바를 빌드하므로 AutoShowBpmFromSong은 fallback으로만 사용)
            if (firstBtn != null)
                OnDifficultyClicked(firstDiff, firstBtn);
            else
                AutoShowBpmFromSong(song);

            ShowDetail(true);
        }

        void AutoShowBpmFromSong(ISongEntry song)
        {
            if (song.Difficulties.Count == 0) return;
            string path = Path.Combine(Application.streamingAssetsPath, song.GetChartPath(song.Difficulties[0]));
            if (!File.Exists(path)) return;
            var result = VmSongParser.Parse(File.ReadAllText(path));
            if (result.Success) BuildBpmBar(result.chart);
        }

        void OnDifficultyClicked(string difficulty, DifficultyButtonUI sender)
        {
            if (_selectedDiffBtn != null) _selectedDiffBtn.SetSelected(false);
            _selectedDiffBtn = sender;
            if (_selectedDiffBtn != null) _selectedDiffBtn.SetSelected(true);

            _selectedDifficulty = difficulty;
            LoadChart();
        }

        void LoadChart()
        {
            if (_selectedSong == null || _selectedDifficulty == null) return;

            string path = Path.Combine(Application.streamingAssetsPath, _selectedSong.GetChartPath(_selectedDifficulty));
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[RhythmGame] Chart not found: {path}");
                return;
            }

            var result = VmSongParser.Parse(File.ReadAllText(path));
            if (!result.Success) return;

            _loadedChart = result.chart;
            BuildBpmBar(_loadedChart);
        }

        void BuildBpmBar(VmSongChart chart)
        {
            if (bpmBar == null) return;
            ClearChildren(bpmBar);
            bpmBar.gameObject.SetActive(true);

            _baseBpm = chart.tempoMap?.segments?.Count > 0
                ? chart.tempoMap.segments[0].bpm
                : 120f;
            _bpmOffset = 0;

            MakeBpmOffsetBtn(-5);
            MakeBpmOffsetBtn(-1);
            _bpmLabel = MakeBpmLabel();
            MakeBpmOffsetBtn(+1);
            MakeBpmOffsetBtn(+5);
            UpdateBpmLabel();
        }

        void MakeBpmOffsetBtn(int delta)
        {
            var go = new GameObject($"BPMBtn{(delta > 0 ? "+" : "")}{delta}");
            go.transform.SetParent(bpmBar, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 36f;
            le.preferredHeight = 36f;
            go.AddComponent<CanvasRenderer>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.28f, 0.45f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.45f, 0.65f, 1f);
            colors.pressedColor     = new Color(0.12f, 0.18f, 0.3f, 1f);
            btn.colors = colors;

            var lc  = new GameObject("Label");
            lc.transform.SetParent(go.transform, false);
            var crt = lc.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            lc.AddComponent<CanvasRenderer>();
            var t = lc.AddComponent<TextMeshProUGUI>();
            t.text      = (delta > 0 ? "+" : "") + delta;
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize  = 11;
            t.color     = Color.white;

            int d = delta;
            btn.onClick.AddListener(() => { _bpmOffset += d; UpdateBpmLabel(); });
        }

        TextMeshProUGUI MakeBpmLabel()
        {
            var go = new GameObject("BPMLabel");
            go.transform.SetParent(bpmBar, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth   = 1f;
            le.preferredHeight = 36f;
            go.AddComponent<CanvasRenderer>();
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize  = 12;
            t.color     = Color.white;
            return t;
        }

        void UpdateBpmLabel()
        {
            if (_bpmLabel != null)
                _bpmLabel.text = $"{_baseBpm + _bpmOffset:F0} BPM";
        }

        void HideBpmBar()
        {
            if (bpmBar != null)
            {
                bpmBar.gameObject.SetActive(false);
                ClearChildren(bpmBar);
            }
            _bpmLabel = null;
        }

        void ShowDetail(bool show)
        {
            if (noSongPanel != null)       noSongPanel.SetActive(!show);
            if (songSelectedPanel != null) songSelectedPanel.SetActive(show);
        }

        public void OnPreviewButtonClicked() { }

        public void OnPlayButtonClicked()
        {
            if (_currentInstrument == null || _selectedSong == null ||
                _selectedDifficulty == null || _loadedChart == null) return;

            var host = _currentInstrument.InstrumentRoot.GetComponentInChildren<RhythmGameHost>();
            if (host == null) return;

            // FindObjectsByType<NoteDisplayPanel> fallback 제거 — host의 SerializedField에
            // noteDisplayPanel을 직접 박제하거나, 악기 본인이 자식 INoteDisplayController를 제공한다.

            // 이전 세션 구독 정리
            if (_activeHost != null)
                _activeHost.SessionEnded -= OnSessionEnded;
            _activeHost = host;
            host.SessionEnded += OnSessionEnded;

            // BPM 오프셋 적용: tempoMap 세그먼트 BPM을 사용자 선택 값으로 교체
            float effectiveBpm = _baseBpm + _bpmOffset;
            if (_loadedChart.tempoMap.segments.Count > 0)
            {
                var seg = _loadedChart.tempoMap.segments[0];
                seg.bpm = effectiveBpm;
                _loadedChart.tempoMap.segments[0] = seg;
            }

            int judgedChannel = FindJudgedChannel(_loadedChart, _currentInstrument.InstrumentId);

            RhythmSong rhythmSong = null;
            if (host.SongDatabase != null)
            {
                foreach (var s in host.SongDatabase.songs)
                {
                    if (s != null && s.songId == _selectedSong.SongId)
                    {
                        rhythmSong = s;
                        break;
                    }
                }
            }

            var accompaniment = new Dictionary<int, bool>();
            foreach (var entry in _loadedChart.channelMap.entries)
                if (entry.channel != judgedChannel)
                    accompaniment[entry.channel] = true;

            host.StartSession(_loadedChart, rhythmSong, judgedChannel, accompaniment);
            GameStarted?.Invoke();
        }

        void OnSessionEnded()
        {
            if (_activeHost != null)
            {
                _activeHost.SessionEnded -= OnSessionEnded;
                _activeHost = null;
            }
            GameEnded?.Invoke();
        }

        public void Inject(UnityEngine.Object providerObj, UnityEngine.Object catalogObj)
        {
            // 기존 구독 해제 (idempotent)
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;

            activeInstrumentProviderObject = providerObj;
            songCatalogObject              = catalogObj;
            _provider          = activeInstrumentProviderObject as IActiveInstrumentProvider;
            _catalog           = songCatalogObject as ISongCatalog;
            _currentInstrument = _provider?.Current;

            // isActiveAndEnabled 여부와 무관하게 구독 등록 (가설 2 수정):
            // Inject -> SetActive(true) -> OnEnable 순서에서 OnEnable의 중복 -= +=가 안전하게 처리된다.
            if (_provider != null)
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;

            ResetSelection();
            RefreshSongList();
        }

        void ResetSelection()
        {
            _selectedSong      = null;
            _selectedDifficulty = null;
            _loadedChart       = null;
            _bpmOffset         = 0;
            _selectedDiffBtn   = null;
            ClearChildren(difficultyContainer);
            HideBpmBar();
            ShowDetail(false);
        }

        static int FindJudgedChannel(VmSongChart chart, string instrumentId)
        {
            foreach (var entry in chart.channelMap.entries)
                if (string.Equals(entry.instrumentKey, instrumentId, System.StringComparison.OrdinalIgnoreCase))
                    return entry.channel;
            return -1;
        }

        static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
