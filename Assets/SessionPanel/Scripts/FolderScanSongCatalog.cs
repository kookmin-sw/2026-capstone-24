using System.Collections.Generic;
using System.IO;
using System.Linq;
using RhythmGame.Data;
using UnityEngine;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Folder Scan Song Catalog")]
    public class FolderScanSongCatalog : MonoBehaviour, ISongCatalog
    {
        [SerializeField] string songsRelativePath = "Songs";

        readonly List<ISongEntry> _songs = new List<ISongEntry>();
        IReadOnlyList<ISongEntry> _readOnly;

        public IReadOnlyList<ISongEntry> Songs => _readOnly;
        public event System.Action Changed;

        void Awake()
        {
            var entries = BuildEntriesFromFolder(
                Path.Combine(Application.streamingAssetsPath, songsRelativePath),
                songsRelativePath);
            _songs.AddRange(entries);
            _readOnly = _songs.AsReadOnly();
        }

        public static List<ISongEntry> BuildEntriesFromFolder(string absoluteFolderPath, string relativePrefix)
        {
            var result = new List<ISongEntry>();
            if (!Directory.Exists(absoluteFolderPath)) return result;

            var files = Directory.EnumerateFiles(absoluteFolderPath, "*.vmsong", SearchOption.TopDirectoryOnly)
                                 .OrderBy(p => Path.GetFileName(p), System.StringComparer.InvariantCultureIgnoreCase);

            foreach (var path in files)
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                var parseResult = VmSongParser.Parse(text);
                if (!parseResult.Success || parseResult.chart == null) continue;

                var chart = parseResult.chart;
                var fileName = Path.GetFileName(path);
                var nameNoExt = Path.GetFileNameWithoutExtension(path);
                var songId = string.IsNullOrEmpty(chart.songId) ? nameNoExt : chart.songId;
                var title = string.IsNullOrEmpty(chart.title) ? nameNoExt : chart.title;
                var artist = chart.artist ?? string.Empty;

                // 차트 파일의 instrument 키와 악기 prefab의 instrumentId 사이 케이스 차이를
                // 흡수하기 위해 OrdinalIgnoreCase 비교를 사용한다 (예: chart "piano" ↔ prefab "Piano").
                var instruments = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                if (chart.channelMap != null && chart.channelMap.entries != null)
                    foreach (var e in chart.channelMap.entries)
                        if (!string.IsNullOrEmpty(e.instrumentKey))
                            instruments.Add(e.instrumentKey);

                var chartRelPath = (relativePrefix + "/" + fileName).Replace("\\", "/");
                result.Add(new SongEntryImpl(songId, title, artist, instruments, chartRelPath));
            }
            return result;
        }

        sealed class SongEntryImpl : ISongEntry
        {
            static readonly IReadOnlyList<string> _difficulties = new[] { "Easy", "Normal", "Hard" };
            readonly string _id, _title, _artist, _chartPath;
            readonly HashSet<string> _instruments;

            public SongEntryImpl(string id, string title, string artist, HashSet<string> instruments, string chartPath)
            { _id = id; _title = title; _artist = artist; _instruments = instruments; _chartPath = chartPath; }

            public string SongId => _id;
            public string Title => _title;
            public string Artist => _artist;
            public IReadOnlyList<string> Difficulties => _difficulties;
            public IReadOnlyCollection<string> SupportedInstrumentIds => _instruments;
            public string GetChartPath(string difficulty) => _chartPath;
        }
    }
}
