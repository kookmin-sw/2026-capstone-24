using System;
using System.Collections;
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

        [Header("Instrument Toggles")]
        [SerializeField] Transform instrumentToggleContainer;
        [SerializeField] GameObject instrumentToggleButtonPrefab;

        [Header("Dependencies")]
        [SerializeField] UnityEngine.Object activeInstrumentProviderObject;
        [SerializeField] UnityEngine.Object songCatalogObject;

        [Header("Input Mode Readiness (sub-spec 17)")]
        [SerializeField] XRInputModeProbe inputModeProbe;
        [SerializeField] InputModeReadinessNotice readinessNoticePrefab;

        IActiveInstrumentProvider _provider;
        ISongCatalog _catalog;
        IActiveInstrument _currentInstrument;

        ISongEntry _selectedSong;
        string _selectedDifficulty;
        VmSongChart _loadedChart;

        // channel int -> parsed chart for that instrument (LoadChart fills, ResetSelection clears)
        Dictionary<int, VmSongChart> _otherInstrumentCharts = new Dictionary<int, VmSongChart>();

        float _baseBpm = 120f;
        int _bpmOffset = 0;
        TextMeshProUGUI _bpmLabel;
        DifficultyButtonUI _selectedDiffBtn;

        Dictionary<string, bool> _instrumentToggleStates =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        RhythmGameHost _activeHost;

        InputModeReadinessNotice _readinessNoticeInstance;
        GameObject _readinessInlineOverlay;
        Coroutine _readinessPollCoroutine;

        public event System.Action GameStarted;
        public event System.Action GameEnded;

        void Awake()
        {
            _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
            _catalog  = songCatalogObject as ISongCatalog;

            // SerializeField가 미할당인 경우 씬에서 자동 탐색 (scene wiring 대안)
            if (inputModeProbe == null)
                inputModeProbe = FindObjectOfType<XRInputModeProbe>();

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
            if (_provider == null)
                _provider = activeInstrumentProviderObject as IActiveInstrumentProvider;
            if (_catalog == null)
                _catalog = songCatalogObject as ISongCatalog;

            if (_provider != null)
            {
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
            }
            if (_catalog != null)
            {
                _catalog.Changed -= OnCatalogChanged;
                _catalog.Changed += OnCatalogChanged;
            }
            _currentInstrument = _provider?.Current;
            RefreshSongList();
        }

        void OnDisable()
        {
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
            if (_catalog != null)
                _catalog.Changed -= OnCatalogChanged;
            StopReadinessGate();
        }

        void OnCatalogChanged() => RefreshSongList();

        void OnActiveInstrumentChanged(IActiveInstrument instrument)
        {
            _currentInstrument = instrument;
            StopReadinessGate();
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
            foreach (var diff in song.GetDifficultiesFor(_currentInstrument.InstrumentId))
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
                    btn.SetLabel(MapDifficultyLabel(diff));
                    if (firstBtn == null) { firstBtn = btn; firstDiff = diff; }
                }
            }

            if (firstBtn != null)
                OnDifficultyClicked(firstDiff, firstBtn);
            else
                AutoShowBpmFromSong(song);

            ShowDetail(true);
        }

        void AutoShowBpmFromSong(ISongEntry song)
        {
            var __diffs = song.GetDifficultiesFor(_currentInstrument?.InstrumentId ?? string.Empty);
            if (__diffs.Count == 0) return;
            var __firstDiff = System.Linq.Enumerable.First(__diffs);
            string text = _catalog?.GetChartText(song.GetChartPath(_currentInstrument.InstrumentId, __firstDiff));
            if (string.IsNullOrEmpty(text)) return;
            var result = VmSongParser.Parse(text);
            if (result.Success) BuildBpmBar(result.chart);
        }

        void OnDifficultyClicked(string difficulty, DifficultyButtonUI sender)
        {
            if (_selectedDiffBtn != null) _selectedDiffBtn.SetSelected(false);
            _selectedDiffBtn = sender;
            if (_selectedDiffBtn != null) _selectedDiffBtn.SetSelected(true);

            _selectedDifficulty = difficulty;
            LoadChart();
            BuildInstrumentToggles();
        }

        void LoadChart()
        {
            if (_selectedSong == null || _selectedDifficulty == null) return;

            string text = _catalog?.GetChartText(_selectedSong.GetChartPath(_currentInstrument.InstrumentId, _selectedDifficulty));
            if (string.IsNullOrEmpty(text)) return;

            var result = VmSongParser.Parse(text);
            if (!result.Success) return;

            _loadedChart = result.chart;
            BuildBpmBar(_loadedChart);

            // Parse charts for other active instruments
            _otherInstrumentCharts.Clear();
            foreach (var otherId in _selectedSong.SupportedInstrumentIds)
            {
                if (string.Equals(otherId, _currentInstrument.InstrumentId, StringComparison.OrdinalIgnoreCase)) continue;
                string otherRel = _selectedSong.GetChartPath(otherId, _selectedDifficulty);
                if (string.IsNullOrEmpty(otherRel)) continue;
                string otherText = _catalog?.GetChartText(otherRel);
                if (string.IsNullOrEmpty(otherText)) continue;
                var otherResult = VmSongParser.Parse(otherText);
                if (!otherResult.Success || otherResult.chart.channelMap.entries.Count == 0) continue;
                int firstChannel = otherResult.chart.channelMap.entries[0].channel;
                _otherInstrumentCharts[firstChannel] = otherResult.chart;
            }
        }

        void BuildInstrumentToggles()
        {
            _instrumentToggleStates.Clear();

            if (_selectedSong == null || _selectedDifficulty == null || _currentInstrument == null) return;
            if (instrumentToggleContainer == null) return;

            ClearChildren(instrumentToggleContainer);

            var candidates = new List<string>();
            foreach (var otherId in _selectedSong.SupportedInstrumentIds)
            {
                if (string.Equals(otherId, _currentInstrument.InstrumentId, StringComparison.OrdinalIgnoreCase))
                    continue;
                var diffs = _selectedSong.GetDifficultiesFor(otherId);
                bool hasDiff = false;
                foreach (var d in diffs)
                {
                    if (string.Equals(d, _selectedDifficulty, StringComparison.OrdinalIgnoreCase))
                    {
                        hasDiff = true;
                        break;
                    }
                }
                if (hasDiff) candidates.Add(otherId);
            }

            candidates.Sort(StringComparer.Ordinal);

            if (candidates.Count == 0)
            {
                instrumentToggleContainer.gameObject.SetActive(false);
                return;
            }

            instrumentToggleContainer.gameObject.SetActive(true);

            foreach (var instrumentId in candidates)
            {
                _instrumentToggleStates[instrumentId] = true;

                if (instrumentToggleButtonPrefab == null) continue;

                var go = Instantiate(instrumentToggleButtonPrefab, instrumentToggleContainer);
                var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.preferredHeight = 36f;
                var toggleBtn = go.GetComponent<InstrumentToggleButtonUI>();
                if (toggleBtn != null)
                    toggleBtn.Setup(instrumentId, true, OnInstrumentToggleChanged);
            }
        }

        void OnInstrumentToggleChanged(string instrumentId, bool isOn)
        {
            _instrumentToggleStates[instrumentId] = isOn;
        }

        internal Dictionary<int, bool> BuildAccompanimentDict(int judgedChannel)
        {
            var accompaniment = new Dictionary<int, bool>();
            if (_loadedChart == null) return accompaniment;
            foreach (var entry in _loadedChart.channelMap.entries)
            {
                if (entry.channel == judgedChannel) continue;
                bool on = true;
                if (_instrumentToggleStates.TryGetValue(entry.instrumentKey ?? string.Empty, out var stored))
                    on = stored;
                accompaniment[entry.channel] = on;
            }
            return accompaniment;
        }

        internal Dictionary<int, bool> BuildAccompanimentDictFromChart(int judgedChannel, VmSongChart sourceChart)
        {
            var dict = new Dictionary<int, bool>();
            if (sourceChart == null) return dict;
            foreach (var entry in sourceChart.channelMap.entries)
            {
                if (entry.channel == judgedChannel) continue;
                bool on = true;
                if (_instrumentToggleStates.TryGetValue(entry.instrumentKey ?? string.Empty, out var stored))
                    on = stored;
                dict[entry.channel] = on;
            }
            return dict;
        }

        internal VmSongChart BuildMergedChartForSession(float effectiveBpm)
        {
            if (_loadedChart == null) return null;

            // Shallow in-memory clone of player chart
            var merged = new VmSongChart
            {
                title  = _loadedChart.title,
                artist = _loadedChart.artist,
                songId = _loadedChart.songId,
            };
            merged.tempoMap.ticksPerQuarter = _loadedChart.tempoMap.ticksPerQuarter;
            foreach (var seg in _loadedChart.tempoMap.segments) merged.tempoMap.segments.Add(seg);
            foreach (var e   in _loadedChart.channelMap.entries) merged.channelMap.entries.Add(e);
            foreach (var t   in _loadedChart.tracks)             merged.tracks.Add(t);

            // Merge active instrument charts (dedup by channel)
            var seen = new HashSet<int>();
            foreach (var e in merged.channelMap.entries) seen.Add(e.channel);
            foreach (var kv in _otherInstrumentCharts)
            {
                foreach (var entry in kv.Value.channelMap.entries)
                {
                    if (seen.Add(entry.channel)) merged.channelMap.entries.Add(entry);
                }
                foreach (var track in kv.Value.tracks) merged.tracks.Add(track);
            }

            // Stamp effectiveBpm into the merged chart first tempo segment (struct reassignment)
            if (merged.tempoMap.segments.Count > 0)
            {
                var seg = merged.tempoMap.segments[0];
                seg.bpm = effectiveBpm;
                merged.tempoMap.segments[0] = seg;
            }
            return merged;
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
            var go = new GameObject("BPMBtn" + (delta > 0 ? "+" : "") + delta);
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
                _bpmLabel.text = (_baseBpm + _bpmOffset).ToString("F0") + " BPM";
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

            // === readiness gate (sub-spec 17) ===
            if (!TryGateInputMode())
            {
                if (_readinessInlineOverlay == null)
                {
                    var required = (_currentInstrument as InstrumentBase)?.RequiredInputMode ?? InputMode.Any;
                    ShowInlineReadinessNotice(required);
                }
                if (_readinessPollCoroutine == null)
                    _readinessPollCoroutine = StartCoroutine(PollReadinessAndAutoStart());
                return;
            }
            // === gate end ===

            StartSessionInternal();
        }

        bool TryGateInputMode()
        {
            var ib = _currentInstrument as InstrumentBase;
            if (ib == null) return true;
            var required = ib.RequiredInputMode;
            if (required == InputMode.Any) return true;
            var current = inputModeProbe != null ? inputModeProbe.Current : InputMode.Any;
            if (current == InputMode.Any) return true; // 모드 미감지 — PASS
            return current == required;
        }

        IEnumerator PollReadinessAndAutoStart()
        {
            while (true)
            {
                yield return null;
                if (TryGateInputMode())
                {
                    HideInlineReadinessNotice();
                    _readinessPollCoroutine = null;
                    StartSessionInternal();
                    yield break;
                }
            }
        }

        void ShowInlineReadinessNotice(InputMode required)
        {
            if (_readinessInlineOverlay != null) return;

            // 패널 내 기존 TMP에서 폰트 참조 (한국어 폰트 재사용)
            var srcTmp = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

            var overlayGO = new GameObject("_ReadinessNotice");
            overlayGO.transform.SetParent(transform, false);

            var rt = overlayGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            overlayGO.AddComponent<CanvasRenderer>();
            var bg = overlayGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            var msgGO = new GameObject("Msg");
            msgGO.transform.SetParent(overlayGO.transform, false);
            var msgRT = msgGO.AddComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.05f, 0.15f);
            msgRT.anchorMax = new Vector2(0.95f, 0.85f);
            msgRT.offsetMin = msgRT.offsetMax = Vector2.zero;
            msgGO.AddComponent<CanvasRenderer>();
            var tmp = msgGO.AddComponent<TMPro.TextMeshProUGUI>();
            if (srcTmp != null) tmp.font = srcTmp.font;
            tmp.text = required == InputMode.HandTracking
                ? "이 악기는 핸드 트래킹으로만\n연주할 수 있어요.\n\nQuest 설정에서 전환해 주세요."
                : "이 악기는 컨트롤러로만\n연주할 수 있어요.\n\n컨트롤러를 잡아 활성화하세요.";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize  = 24;
            tmp.color     = Color.white;

            _readinessInlineOverlay = overlayGO;
        }

        void HideInlineReadinessNotice()
        {
            if (_readinessInlineOverlay != null)
            {
                Destroy(_readinessInlineOverlay);
                _readinessInlineOverlay = null;
            }
        }

        void StopReadinessGate()
        {
            if (_readinessPollCoroutine != null)
            {
                StopCoroutine(_readinessPollCoroutine);
                _readinessPollCoroutine = null;
            }
            HideInlineReadinessNotice();
            _readinessNoticeInstance?.Hide();
        }

        void StartSessionInternal()
        {
            var host = _currentInstrument.InstrumentRoot.GetComponentInChildren<RhythmGameHost>();
            if (host == null) return;

            if (_activeHost != null)
                _activeHost.SessionEnded -= OnSessionEnded;
            _activeHost = host;
            host.SessionEnded += OnSessionEnded;

            float effectiveBpm = _baseBpm + _bpmOffset;
            var merged = BuildMergedChartForSession(effectiveBpm);

            int judgedChannel = FindJudgedChannel(merged, _currentInstrument.InstrumentId);

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

            var accompaniment = BuildAccompanimentDictFromChart(judgedChannel, merged);

            host.StartSession(merged, rhythmSong, judgedChannel, accompaniment);
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
            if (_provider != null)
                _provider.ActiveInstrumentChanged -= OnActiveInstrumentChanged;
            if (_catalog != null)
                _catalog.Changed -= OnCatalogChanged;

            activeInstrumentProviderObject = providerObj;
            songCatalogObject              = catalogObj;
            _provider          = activeInstrumentProviderObject as IActiveInstrumentProvider;
            _catalog           = songCatalogObject as ISongCatalog;
            _currentInstrument = _provider?.Current;

            if (_provider != null)
                _provider.ActiveInstrumentChanged += OnActiveInstrumentChanged;
            if (_catalog != null)
                _catalog.Changed += OnCatalogChanged;

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
            _instrumentToggleStates.Clear();
            _otherInstrumentCharts.Clear();
            ClearChildren(difficultyContainer);
            HideBpmBar();
            if (instrumentToggleContainer != null)
            {
                ClearChildren(instrumentToggleContainer);
                instrumentToggleContainer.gameObject.SetActive(false);
            }
            ShowDetail(false);
        }

        static string MapDifficultyLabel(string diff)
        {
            switch (diff)
            {
                case "1": return "Easy";
                case "2": return "Normal";
                case "3": return "Hard";
                default:  return diff;
            }
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
