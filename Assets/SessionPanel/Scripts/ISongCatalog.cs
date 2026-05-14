using System.Collections.Generic;

namespace SessionPanel
{
    public interface ISongCatalog
    {
        IReadOnlyList<ISongEntry> Songs { get; }
        event System.Action Changed;
    }

    public interface ISongEntry
    {
        string SongId { get; }
        string Title { get; }
        string Artist { get; }
        IReadOnlyList<string> Difficulties { get; }
        IReadOnlyCollection<string> SupportedInstrumentIds { get; }
        string GetChartPath(string difficulty);
    }
}
