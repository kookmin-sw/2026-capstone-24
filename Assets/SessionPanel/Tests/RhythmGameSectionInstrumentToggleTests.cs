using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Instruments;
using RhythmGame.Data;

namespace SessionPanel
{
    public class RhythmGameSectionInstrumentToggleTests
    {
        // --------------- Stubs ---------------

        class StubSongEntry : ISongEntry
        {
            public string SongId { get; }
            public string Title  { get; }
            public string Artist => "";
            public IReadOnlyList<string> Difficulties { get; } = new List<string>();
            readonly HashSet<string> _supportedIds;

            readonly Dictionary<string, List<string>> _diffs =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            public IReadOnlyCollection<string> SupportedInstrumentIds => _supportedIds;

            public StubSongEntry(string id, params string[] supportedInstrumentIds)
            {
                SongId = id;
                Title  = id;
                _supportedIds = new HashSet<string>(supportedInstrumentIds, StringComparer.OrdinalIgnoreCase);
            }

            public void AddDifficulties(string instrumentId, params string[] difficulties)
            {
                if (!_diffs.TryGetValue(instrumentId, out var list))
                    _diffs[instrumentId] = list = new List<string>();
                list.AddRange(difficulties);
            }

            public IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)
            {
                if (_diffs.TryGetValue(instrumentId, out var list)) return list;
                return new string[0];
            }

            public string GetChartPath(string instrumentId, string difficulty) => null;
            public string GetChartPath(string difficulty) => null;
        }

        class StubCatalog : MonoBehaviour, ISongCatalog
        {
            readonly List<ISongEntry> _songs = new List<ISongEntry>();
            public IReadOnlyList<ISongEntry> Songs => _songs;
            public event Action Changed;
            public void Add(ISongEntry e) => _songs.Add(e);
            public string GetChartText(string relPath) => null;
        }

        class StubInstrument : IActiveInstrument
        {
            public Transform PanelAnchor    => null;
            public Transform InstrumentRoot => null;
            public float InstanceVolume { get; set; }
            public string InstrumentId { get; }
            public StubInstrument(string id) => InstrumentId = id;
        }

        class StubProvider : MonoBehaviour, IActiveInstrumentProvider
        {
            public event Action<IActiveInstrument> ActiveInstrumentChanged;
            public IActiveInstrument Current { get; private set; }
            public void SetCurrent(IActiveInstrument inst) { Current = inst; }
            public void RaiseChanged(IActiveInstrument inst) { Current = inst; ActiveInstrumentChanged?.Invoke(inst); }
        }

        // --------------- Builder ---------------

        static (RhythmGameSectionController ctrl, Transform toggleContainer, StubProvider provider, GameObject root)
            BuildControllerWithToggleContainer()
        {
            var root      = new GameObject("TestRoot");
            var canvasGo  = new GameObject("Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(root.transform, false);

            var listGo = new GameObject("SongListContent", typeof(RectTransform));
            listGo.transform.SetParent(canvasGo.transform, false);

            var providerGo = new GameObject("Provider");
            providerGo.transform.SetParent(root.transform, false);
            var provider   = providerGo.AddComponent<StubProvider>();

            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<RhythmGameSectionController>();
            // songListContent + songRowPrefab (minimal)
            var rowPrefab = new GameObject("SongRowPrefab");
            rowPrefab.AddComponent<RectTransform>();
            rowPrefab.AddComponent<Image>();
            var rowBtn = rowPrefab.AddComponent<Button>();
            rowPrefab.AddComponent<SongRowUI>();
            var soRow = new UnityEditor.SerializedObject(rowPrefab.GetComponent<SongRowUI>());
            soRow.FindProperty("button").objectReferenceValue = rowBtn;
            soRow.ApplyModifiedPropertiesWithoutUndo();

            // difficultyContainer + difficultyButtonPrefab (minimal)
            var diffContainerGo = new GameObject("DiffContainer", typeof(RectTransform));
            diffContainerGo.transform.SetParent(canvasGo.transform, false);

            var diffBtnPrefab = new GameObject("DiffBtnPrefab");
            diffBtnPrefab.AddComponent<RectTransform>();
            diffBtnPrefab.AddComponent<Image>();
            var diffBtn2 = diffBtnPrefab.AddComponent<Button>();
            diffBtnPrefab.AddComponent<DifficultyButtonUI>();
            var soDiff = new UnityEditor.SerializedObject(diffBtnPrefab.GetComponent<DifficultyButtonUI>());
            soDiff.FindProperty("button").objectReferenceValue = diffBtn2;
            soDiff.ApplyModifiedPropertiesWithoutUndo();

            // instrumentToggleContainer
            var toggleContainerGo = new GameObject("ToggleContainer", typeof(RectTransform));
            toggleContainerGo.transform.SetParent(canvasGo.transform, false);

            // instrumentToggleButtonPrefab (minimal: Toggle + InstrumentToggleButtonUI)
            var toggleBtnPrefab = new GameObject("ToggleBtnPrefab");
            toggleBtnPrefab.AddComponent<RectTransform>();
            var tog = toggleBtnPrefab.AddComponent<Toggle>();
            var toggleUi = toggleBtnPrefab.AddComponent<InstrumentToggleButtonUI>();
            var soTog = new UnityEditor.SerializedObject(toggleUi);
            soTog.FindProperty("toggle").objectReferenceValue = tog;
            soTog.ApplyModifiedPropertiesWithoutUndo();

            // wire controller via SerializedObject
            var so = new UnityEditor.SerializedObject(ctrl);
            so.FindProperty("songListContent").objectReferenceValue         = listGo.transform;
            so.FindProperty("songRowPrefab").objectReferenceValue           = rowPrefab;
            so.FindProperty("difficultyContainer").objectReferenceValue     = diffContainerGo.transform;
            so.FindProperty("difficultyButtonPrefab").objectReferenceValue  = diffBtnPrefab;
            so.FindProperty("instrumentToggleContainer").objectReferenceValue    = toggleContainerGo.transform;
            so.FindProperty("instrumentToggleButtonPrefab").objectReferenceValue = toggleBtnPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Keep controller inactive to avoid Awake/OnEnable side effects
            ctrlGo.SetActive(false);

            return (ctrl, toggleContainerGo.transform, provider, root);
        }

        static FieldInfo GetPrivateField(string name)
            => typeof(RhythmGameSectionController)
               .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        // Directly inject the 3 state fields needed by BuildInstrumentToggles
        static void InjectState(RhythmGameSectionController ctrl,
            ISongEntry song, string difficulty, IActiveInstrument instrument)
        {
            GetPrivateField("_selectedSong").SetValue(ctrl, song);
            GetPrivateField("_selectedDifficulty").SetValue(ctrl, difficulty);
            GetPrivateField("_currentInstrument").SetValue(ctrl, instrument);
        }

        static void InvokeBuildInstrumentToggles(RhythmGameSectionController ctrl)
        {
            var mi = typeof(RhythmGameSectionController)
                .GetMethod("BuildInstrumentToggles", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(ctrl, null);
        }

        static Dictionary<int, bool> InvokeBuildAccompanimentDict(
            RhythmGameSectionController ctrl, int judgedChannel)
        {
            var mi = typeof(RhythmGameSectionController)
                .GetMethod("BuildAccompanimentDict",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return (Dictionary<int, bool>)mi.Invoke(ctrl, new object[] { judgedChannel });
        }

        // --------------- Tests ---------------

        [Test]
        public void InstrumentToggles_OnlyOtherInstrumentsWithMatchingDifficulty_Built()
        {
            var (ctrl, toggleContainer, provider, root) = BuildControllerWithToggleContainer();
            try
            {
                // piano(easy,normal), drum(easy), violin(easy,hard). player=piano, difficulty=easy
                var song = new StubSongEntry("TestSong", "piano", "drum", "violin");
                song.AddDifficulties("piano",  "easy", "normal");
                song.AddDifficulties("drum",   "easy");
                song.AddDifficulties("violin", "easy", "hard");

                InjectState(ctrl, song, "easy", new StubInstrument("piano"));
                InvokeBuildInstrumentToggles(ctrl);

                // drum(easy) + violin(easy) = 2 candidates, Ordinal sort: "drum" < "violin"
                Assert.AreEqual(2, toggleContainer.childCount,
                    "Should have 2 toggle rows: drum and violin");

                var first  = toggleContainer.GetChild(0).GetComponent<InstrumentToggleButtonUI>();
                var second = toggleContainer.GetChild(1).GetComponent<InstrumentToggleButtonUI>();
                Assert.IsNotNull(first,  "First toggle should have InstrumentToggleButtonUI");
                Assert.IsNotNull(second, "Second toggle should have InstrumentToggleButtonUI");

                // Verify _instrumentToggleStates has both drum and violin = true
                var states = (Dictionary<string, bool>)GetPrivateField("_instrumentToggleStates").GetValue(ctrl);
                Assert.IsTrue(states.ContainsKey("drum"),   "drum key should be in states");
                Assert.IsTrue(states.ContainsKey("violin"), "violin key should be in states");
                Assert.IsTrue(states["drum"],   "drum should be ON by default");
                Assert.IsTrue(states["violin"], "violin should be ON by default");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void AccompanimentDict_ReflectsToggleStates_OffChannelFalse()
        {
            var (ctrl, toggleContainer, provider, root) = BuildControllerWithToggleContainer();
            try
            {
                // piano(easy), drum(easy). player=piano
                var song = new StubSongEntry("TestSong2", "piano", "drum");
                song.AddDifficulties("piano", "easy");
                song.AddDifficulties("drum",  "easy");

                InjectState(ctrl, song, "easy", new StubInstrument("piano"));
                InvokeBuildInstrumentToggles(ctrl);
                // _instrumentToggleStates = {drum: true}

                // Set up _loadedChart with channelMap: {1:"piano", 2:"drum"}
                var chart = new VmSongChart();
                var seg = new TempoSegment();
                seg.bpm = 120f;
                chart.tempoMap.segments.Add(seg);
                chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry { channel = 1, instrumentKey = "piano" });
                chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry { channel = 2, instrumentKey = "drum"  });

                GetPrivateField("_loadedChart").SetValue(ctrl, chart);

                // Simulate drum toggle OFF
                var onToggleChanged = typeof(RhythmGameSectionController)
                    .GetMethod("OnInstrumentToggleChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                onToggleChanged.Invoke(ctrl, new object[] { "drum", false });

                // BuildAccompanimentDict(judgedChannel=1) => {2: false}
                var dict = InvokeBuildAccompanimentDict(ctrl, 1);

                Assert.IsFalse(dict.ContainsKey(1), "judgedChannel 1 should NOT be in dict");
                Assert.IsTrue(dict.ContainsKey(2),  "channel 2 (drum) should be in dict");
                Assert.IsFalse(dict[2], "drum channel should be false (OFF)");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void InstrumentToggles_PlayerOnlySong_ContainerDeactivated()
        {
            var (ctrl, toggleContainer, provider, root) = BuildControllerWithToggleContainer();
            try
            {
                // Only piano exists. player=piano.
                var song = new StubSongEntry("SoloSong", "piano");
                song.AddDifficulties("piano", "easy");

                InjectState(ctrl, song, "easy", new StubInstrument("piano"));
                InvokeBuildInstrumentToggles(ctrl);

                Assert.IsFalse(toggleContainer.gameObject.activeSelf,
                    "Toggle container should be inactive when no other instruments");
                Assert.AreEqual(0, toggleContainer.childCount,
                    "Toggle container should have no children");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void InstrumentToggles_DifficultyChange_RebuildsList()
        {
            var (ctrl, toggleContainer, provider, root) = BuildControllerWithToggleContainer();
            try
            {
                // piano(easy,hard), drum(easy), violin(hard). player=piano
                var song = new StubSongEntry("MultiDiffSong", "piano", "drum", "violin");
                song.AddDifficulties("piano",  "easy", "hard");
                song.AddDifficulties("drum",   "easy");
                song.AddDifficulties("violin", "hard");

                var instrument = new StubInstrument("piano");

                // First: easy difficulty
                InjectState(ctrl, song, "easy", instrument);
                InvokeBuildInstrumentToggles(ctrl);

                var states = (Dictionary<string, bool>)GetPrivateField("_instrumentToggleStates").GetValue(ctrl);
                Assert.IsTrue(states.ContainsKey("drum"),    "drum should be in states for easy");
                Assert.IsFalse(states.ContainsKey("violin"), "violin should NOT be in states for easy");

                // Switch to hard: only change _selectedDifficulty + rebuild.
                // In EditMode, ClearChildren uses Destroy (deferred), so we manually
                // DestroyImmediate existing children before calling BuildInstrumentToggles
                // to avoid residual children from the previous easy-difficulty build.
                for (int i = toggleContainer.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(toggleContainer.GetChild(i).gameObject);
                GetPrivateField("_selectedDifficulty").SetValue(ctrl, "hard");
                InvokeBuildInstrumentToggles(ctrl);

                states = (Dictionary<string, bool>)GetPrivateField("_instrumentToggleStates").GetValue(ctrl);
                Assert.IsFalse(states.ContainsKey("drum"),   "drum should NOT be in states for hard");
                Assert.IsTrue(states.ContainsKey("violin"),  "violin should be in states for hard");
                Assert.AreEqual(1, toggleContainer.childCount,
                    "Only 1 toggle row (violin) for hard difficulty");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
