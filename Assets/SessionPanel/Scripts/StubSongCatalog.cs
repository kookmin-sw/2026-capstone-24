using System.Collections.Generic;
using RhythmGame.Data;
using UnityEngine;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Stub Song Catalog")]
    public class StubSongCatalog : MonoBehaviour, ISongCatalog
    {
        [SerializeField] public RhythmSongDatabase[] sourceDatabases = System.Array.Empty<RhythmSongDatabase>();

        readonly List<SongEntryImpl> _songs = new();
        IReadOnlyList<ISongEntry> _readOnly;

        public IReadOnlyList<ISongEntry> Songs => _readOnly;
        public event System.Action Changed;

        void Awake()
        {
            BuildCatalog();
        }

        void BuildCatalog()
        {
            _songs.Clear();
            var byId = new Dictionary<string, SongEntryImpl>();

            foreach (var db in sourceDatabases)
            {
                if (db == null) continue;
                foreach (var song in db.songs)
                {
                    if (song == null) continue;
                    string id = song.songId;
                    if (!byId.TryGetValue(id, out var entry))
                    {
                        entry = new SongEntryImpl(song);
                        byId[id] = entry;
                        _songs.Add(entry);
                    }
                    entry.AddSupportedInstrument(db.instrumentKey);
                }
            }

            _readOnly = _songs.AsReadOnly();
        }

        class SongEntryImpl : ISongEntry
        {
            readonly RhythmSong _song;
            readonly HashSet<string> _instrumentIds = new();
            static readonly IReadOnlyList<string> _difficulties = new[] { "Easy", "Normal", "Hard" };

            public SongEntryImpl(RhythmSong song) { _song = song; }
            public void AddSupportedInstrument(string key) { _instrumentIds.Add(key); }

            public string SongId => _song.songId;
            public string Title  => _song.title;
            public string Artist => _song.artist;
            public IReadOnlyList<string> Difficulties => _difficulties;
            public IReadOnlyCollection<string> SupportedInstrumentIds => _instrumentIds;
            public string GetChartPath(string difficulty) => "Songs/test.vmsong";
            public System.Collections.Generic.IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId)
                => System.Array.Empty<string>();
            public string GetChartPath(string instrumentId, string difficulty) => null;
        }
    }
}
