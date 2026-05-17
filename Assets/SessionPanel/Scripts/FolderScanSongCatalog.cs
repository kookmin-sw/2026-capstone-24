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

            var builders = new Dictionary<string, GroupedSongBuilder>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var path in files)
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(path);

                int lastHyphen = nameNoExt.LastIndexOf('-');
                if (lastHyphen < 0) continue;
                string difficulty = nameNoExt.Substring(lastHyphen + 1);
                string beforeDiff = nameNoExt.Substring(0, lastHyphen);

                int secondHyphen = beforeDiff.LastIndexOf('-');
                if (secondHyphen < 0) continue;
                string instrument = beforeDiff.Substring(secondHyphen + 1);
                string songname   = beforeDiff.Substring(0, secondHyphen);

                if (string.IsNullOrEmpty(songname) || string.IsNullOrEmpty(instrument) || string.IsNullOrEmpty(difficulty))
                    continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                var parseResult = VmSongParser.Parse(text);
                if (!parseResult.Success || parseResult.chart == null) continue;

                var chart    = parseResult.chart;
                var fileName = Path.GetFileName(path);
                var relPath  = (relativePrefix + "/" + fileName).Replace("\\", "/");

                if (!builders.TryGetValue(songname, out var builder))
                {
                    var songId = string.IsNullOrEmpty(chart.songId) ? songname : chart.songId;
                    var title  = string.IsNullOrEmpty(chart.title)  ? songname : chart.title;
                    var artist = chart.artist ?? string.Empty;
                    builder = new GroupedSongBuilder(songId, title, artist);
                    builders[songname] = builder;
                }

                builder.TryAdd(instrument, difficulty, relPath);
            }

            foreach (var kvp in builders.OrderBy(kv => kv.Key, System.StringComparer.OrdinalIgnoreCase))
                result.Add(kvp.Value.Build());

            return result;
        }

        sealed class GroupedSongBuilder
        {
            readonly string _id, _title, _artist;
            readonly Dictionary<(string, string), string> _files =
                new Dictionary<(string, string), string>(InstrumentDifficultyComparer.Instance);
            readonly Dictionary<string, List<string>> _diffsByInstrument =
                new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);

            public GroupedSongBuilder(string id, string title, string artist)
            { _id = id; _title = title; _artist = artist; }

            public void TryAdd(string instrument, string difficulty, string relPath)
            {
                var key = (instrument, difficulty);
                if (_files.ContainsKey(key)) return;
                _files[key] = relPath;

                if (!_diffsByInstrument.TryGetValue(instrument, out var list))
                {
                    list = new List<string>();
                    _diffsByInstrument[instrument] = list;
                }
                list.Add(difficulty);
            }

            public ISongEntry Build() =>
                new MultiFileSongEntry(_id, _title, _artist, _files, _diffsByInstrument);
        }

        sealed class InstrumentDifficultyComparer : IEqualityComparer<(string, string)>
        {
            public static readonly InstrumentDifficultyComparer Instance = new InstrumentDifficultyComparer();

            public bool Equals((string, string) x, (string, string) y) =>
                string.Equals(x.Item1, y.Item1, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item2, y.Item2, System.StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string, string) obj)
            {
                int h1 = System.StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1 ?? string.Empty);
                int h2 = System.StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2 ?? string.Empty);
                return h1 ^ (h2 << 5);
            }
        }

        sealed class MultiFileSongEntry : ISongEntry
        {
            static readonly string[] _diffPriority = { "Easy", "Normal", "Hard" };

            readonly string _id, _title, _artist;
            readonly Dictionary<(string, string), string> _files;
            readonly Dictionary<string, List<string>> _diffsByInstrument;
            readonly HashSet<string> _instrumentIds;
            readonly IReadOnlyList<string> _allDifficulties;

            public MultiFileSongEntry(
                string id, string title, string artist,
                Dictionary<(string, string), string> files,
                Dictionary<string, List<string>> diffsByInstrument)
            {
                _id                = id;
                _title             = title;
                _artist            = artist;
                _files             = files;
                _diffsByInstrument = diffsByInstrument;
                _instrumentIds     = new HashSet<string>(diffsByInstrument.Keys, System.StringComparer.OrdinalIgnoreCase);

                var allSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var list in diffsByInstrument.Values)
                    foreach (var d in list) allSet.Add(d);

                var sorted = new List<string>();
                foreach (var p in _diffPriority)
                    if (allSet.Remove(p)) sorted.Add(p);
                sorted.AddRange(allSet.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase));
                _allDifficulties = sorted.AsReadOnly();
            }

            public string SongId  => _id;
            public string Title   => _title;
            public string Artist  => _artist;
            public IReadOnlyList<string>       Difficulties           => _allDifficulties;
            public IReadOnlyCollection<string> SupportedInstrumentIds => _instrumentIds;

            public IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)
            {
                if (_diffsByInstrument.TryGetValue(instrumentId, out var list))
                    return list;
                return System.Array.Empty<string>();
            }

            public string GetChartPath(string instrumentId, string difficulty)
            {
                _files.TryGetValue((instrumentId, difficulty), out var path);
                return path;
            }

            public string GetChartPath(string difficulty)
            {
                foreach (var instrumentId in _instrumentIds)
                {
                    if (_files.TryGetValue((instrumentId, difficulty), out var p))
                        return p;
                }
                return null;
            }
        }
    }
}
