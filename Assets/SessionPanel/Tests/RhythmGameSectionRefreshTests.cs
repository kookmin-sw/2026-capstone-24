using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Instruments;

namespace SessionPanel
{
    public class RhythmGameSectionRefreshTests
    {
        class StubSongEntry : ISongEntry
        {
            public string SongId { get; }
            public string Title { get; }
            public string Artist => "";
            public IReadOnlyList<string> Difficulties { get; } = new List<string> { "Normal" };
            readonly HashSet<string> _ids;
            public IReadOnlyCollection<string> SupportedInstrumentIds => _ids;
            public string GetChartPath(string difficulty) => "";
            public System.Collections.Generic.IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)
                => new string[0];
            public string GetChartPath(string instrumentId, string difficulty) => null;
            public StubSongEntry(string id, params string[] instrumentIds)
            {
                SongId = id; Title = id; _ids = new HashSet<string>(instrumentIds);
            }
        }

        class StubCatalog : MonoBehaviour, ISongCatalog
        {
            readonly List<ISongEntry> _songs = new List<ISongEntry>();
            public IReadOnlyList<ISongEntry> Songs => _songs;
            public event System.Action Changed;
            public void Add(ISongEntry entry) => _songs.Add(entry);
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
            public event System.Action<IActiveInstrument> ActiveInstrumentChanged;
            public IActiveInstrument Current { get; private set; }
            public void SetCurrent(IActiveInstrument inst) { Current = inst; }
            public void RaiseChanged(IActiveInstrument inst) { Current = inst; ActiveInstrumentChanged?.Invoke(inst); }
        }

        static (RhythmGameSectionController ctrl, Transform listContent, StubProvider provider, GameObject root)
            BuildController()
        {
            var root = new GameObject("TestRoot");
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(root.transform, false);
            var listGo = new GameObject("SongListContent", typeof(RectTransform));
            listGo.transform.SetParent(canvasGo.transform, false);
            var providerGo = new GameObject("Provider");
            providerGo.transform.SetParent(root.transform, false);
            var provider = providerGo.AddComponent<StubProvider>();
            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<RhythmGameSectionController>();

            var so = new UnityEditor.SerializedObject(ctrl);
            so.FindProperty("songListContent").objectReferenceValue = listGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var rowPrefab = new GameObject("SongRowPrefab");
            rowPrefab.AddComponent<RectTransform>();
            rowPrefab.AddComponent<Image>();
            var rowBtn = rowPrefab.AddComponent<Button>();
            rowPrefab.AddComponent<SongRowUI>();
            var soRow = new UnityEditor.SerializedObject(rowPrefab.GetComponent<SongRowUI>());
            soRow.FindProperty("button").objectReferenceValue = rowBtn;
            soRow.ApplyModifiedPropertiesWithoutUndo();

            so = new UnityEditor.SerializedObject(ctrl);
            so.FindProperty("songRowPrefab").objectReferenceValue = rowPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (ctrl, listGo.transform, provider, root);
        }

        [Test]
        public void SupportedSong_IsActive_WhenCurrentInstrumentMatches()
        {
            var (ctrl, listContent, provider, root) = BuildController();
            try
            {
                var drumSong  = new StubSongEntry("SongA", "DrumKit");
                var pianoSong = new StubSongEntry("SongB", "Piano");
                var catalogGo = new GameObject("Catalog");
                catalogGo.transform.SetParent(root.transform, false);
                var catalog   = catalogGo.AddComponent<StubCatalog>();
                catalog.Add(drumSong);
                catalog.Add(pianoSong);
                provider.SetCurrent(new StubInstrument("DrumKit"));
                ctrl.gameObject.SetActive(false);
                ctrl.Inject(provider as UnityEngine.Object, catalog as UnityEngine.Object);
                ctrl.gameObject.SetActive(true);
                Assert.AreEqual(2, listContent.childCount, "row count should be 2");
                var drumBtn  = listContent.GetChild(0).GetComponent<Button>();
                var pianoBtn = listContent.GetChild(1).GetComponent<Button>();
                Assert.IsNotNull(drumBtn,  "DrumKit row Button must not be null");
                Assert.IsNotNull(pianoBtn, "Piano row Button must not be null");
                Assert.IsTrue(drumBtn.interactable,   "DrumKit row should be interactable");
                Assert.IsFalse(pianoBtn.interactable, "Piano row should NOT be interactable when DrumKit held");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Inject_BeforeActivation_SubscribesToProviderChange()
        {
            var (ctrl, listContent, provider, root) = BuildController();
            try
            {
                var pianoSong = new StubSongEntry("SongB", "Piano");
                var catalogGo = new GameObject("Catalog");
                catalogGo.transform.SetParent(root.transform, false);
                var catalog   = catalogGo.AddComponent<StubCatalog>();
                catalog.Add(pianoSong);
                ctrl.gameObject.SetActive(false);
                ctrl.Inject(provider as UnityEngine.Object, catalog as UnityEngine.Object);
                provider.RaiseChanged(new StubInstrument("Piano"));
                ctrl.gameObject.SetActive(true);
                Assert.AreEqual(1, listContent.childCount, "Piano song row should exist after activation");
                var pianoBtn = listContent.GetChild(0).GetComponent<Button>();
                Assert.IsNotNull(pianoBtn, "Piano row Button must not be null");
                Assert.IsTrue(pianoBtn.interactable, "Piano row should be interactable when Piano held");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
