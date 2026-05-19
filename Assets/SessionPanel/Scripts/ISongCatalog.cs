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
        /// <summary>
        /// 이 곡에서 어떤 instrument든 한 번이라도 등장한 difficulty의 합집합 (곡별 가변 집합).
        /// </summary>
        IReadOnlyList<string> Difficulties { get; }
        IReadOnlyCollection<string> SupportedInstrumentIds { get; }

        /// <summary>
        /// 그 instrument에 존재하는 difficulty 집합 (OrdinalIgnoreCase).
        /// 빈 집합이면 그 instrument는 이 곡에서 지원되지 않음.
        /// </summary>
        IReadOnlyCollection<string> GetDifficultiesFor(string instrumentId);

        /// <summary>
        /// (instrument, difficulty) -> "Songs/...vmsong" 상대 경로. 매칭 없으면 null.
        /// </summary>
        string GetChartPath(string instrumentId, string difficulty);

        /// <summary>
        /// [Backward-compat] 이 entry의 첫 instrument에 대한 GetChartPath 위임.
        /// 신규 호출자는 GetChartPath(instrumentId, difficulty) 사용 권장.
        /// </summary>
        string GetChartPath(string difficulty);
    }
}
