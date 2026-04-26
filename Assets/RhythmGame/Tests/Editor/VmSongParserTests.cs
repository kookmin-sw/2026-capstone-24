using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class VmSongParserTests
{
    // Plan 001 예제 차트 (공백 주석 포함)
    const string MinimalChart = @"
[Meta]
title=TestSong
artist=TestArtist

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track:1]
tick=0 note=60 len=240 vel=100
tick=240 note=64 len=240 vel=100
";

    // 1. 최소 차트 파싱
    [Test]
    public void MinimalChart_ParsesAllSections()
    {
        var result = VmSongParser.Parse(MinimalChart);

        Assert.IsTrue(result.Success,
            "Errors: " + string.Join(", ", result.errors.ConvertAll(e => e.message)));

        var c = result.chart;
        Assert.AreEqual("TestSong",   c.title);
        Assert.AreEqual("TestArtist", c.artist);
        Assert.AreEqual(480,          c.tempoMap.ticksPerQuarter);
        Assert.AreEqual(1,            c.tempoMap.segments.Count);
        Assert.AreEqual(120f,         c.tempoMap.segments[0].bpm);
        Assert.AreEqual(4,            c.tempoMap.segments[0].beatsPerBar);
        Assert.AreEqual(4,            c.tempoMap.segments[0].beatUnit);
        Assert.AreEqual(1,            c.channelMap.entries.Count);
        Assert.AreEqual("piano",      c.channelMap.entries[0].instrumentKey);
        Assert.AreEqual(1,            c.tracks.Count);
        Assert.AreEqual(2,            c.tracks[0].notes.Count);
        Assert.AreEqual((byte)60,     c.tracks[0].notes[0].midiNote);
    }

    // 2. 결정성: 같은 입력 → 동등한 결과
    [Test]
    public void Determinism_SameInput_EquivalentResult()
    {
        var r1 = VmSongParser.Parse(MinimalChart);
        var r2 = VmSongParser.Parse(MinimalChart);

        Assert.AreEqual(r1.chart.title,                        r2.chart.title);
        Assert.AreEqual(r1.chart.tempoMap.ticksPerQuarter,     r2.chart.tempoMap.ticksPerQuarter);
        Assert.AreEqual(r1.chart.tempoMap.segments.Count,      r2.chart.tempoMap.segments.Count);
        Assert.AreEqual(r1.chart.tracks.Count,                 r2.chart.tracks.Count);
        Assert.AreEqual(r1.chart.tracks[0].notes.Count,        r2.chart.tracks[0].notes.Count);
        Assert.AreEqual(r1.chart.tracks[0].notes[0].tick,      r2.chart.tracks[0].notes[0].tick);
        Assert.AreEqual(r1.chart.tracks[0].notes[0].midiNote,  r2.chart.tracks[0].notes[0].midiNote);
    }

    // 3. channel=10 instrument=drum 보존
    [Test]
    public void DrumChannel10_PreservedInChannelMap()
    {
        const string chart = @"
[Meta]
title=DrumTest

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=10 instrument=drum

[Track:10]
tick=0 note=36 len=120 vel=120
";
        var result = VmSongParser.Parse(chart);
        Assert.IsTrue(result.Success,
            "Errors: " + string.Join(", ", result.errors.ConvertAll(e => e.message)));

        bool found = false;
        foreach (var e in result.chart.channelMap.entries)
            if (e.channel == 10 && e.instrumentKey == "drum") found = true;
        Assert.IsTrue(found, "channel=10 instrument=drum not found in channelMap");
        Assert.AreEqual(RhythmChannels.DrumChannel, 10);
    }

    // 4. 주석 및 빈 줄 무시
    [Test]
    public void Comments_AndBlankLines_Ignored()
    {
        const string withComments = @"
# 이 줄은 주석

[Meta]  # 섹션 뒤 주석
title=CommentTest   # 인라인 주석
artist=Artist

[Resolution]
ticksPerQuarter=480 # 해상도

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4  # 템포

[Channels]
channel=1 instrument=piano

[Track:1]
tick=0 note=60 len=240 vel=100
";
        const string noComments = @"
[Meta]
title=CommentTest
artist=Artist

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track:1]
tick=0 note=60 len=240 vel=100
";
        var r1 = VmSongParser.Parse(withComments);
        var r2 = VmSongParser.Parse(noComments);

        Assert.IsTrue(r1.Success);
        Assert.IsTrue(r2.Success);
        Assert.AreEqual(r2.chart.title,                    r1.chart.title);
        Assert.AreEqual(r2.chart.tempoMap.ticksPerQuarter, r1.chart.tempoMap.ticksPerQuarter);
        Assert.AreEqual(r2.chart.tracks[0].notes.Count,   r1.chart.tracks[0].notes.Count);
    }

    // 5. 대소문자 구분 없이 인식
    [Test]
    public void CaseInsensitive_SectionAndKey()
    {
        const string chart = @"
[META]
Title=CaseTest

[RESOLUTION]
TICKSPERQUARTER=480

[TEMPO]
TICK=0 BPM=120 BEATS=4 BEATUNIT=4

[CHANNELS]
CHANNEL=1 INSTRUMENT=piano

[TRACK:1]
TICK=0 NOTE=60 LEN=240 VEL=100
";
        var result = VmSongParser.Parse(chart);
        Assert.IsTrue(result.Success,
            "Errors: " + string.Join(", ", result.errors.ConvertAll(e => e.message)));
        Assert.AreEqual("CaseTest", result.chart.title);
        Assert.AreEqual(480, result.chart.tempoMap.ticksPerQuarter);
        Assert.AreEqual(1, result.chart.tracks[0].notes.Count);
    }

    // 6. 미지 섹션 → 에러 1개 누적, 나머지는 정상 파싱
    [Test]
    public void UnknownSection_AddsError_ContinuesParsing()
    {
        const string chart = @"
[Meta]
title=ContinueTest

[Resolution]
ticksPerQuarter=480

[UnknownSection]
foo=bar

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track:1]
tick=0 note=60 len=240 vel=100
";
        var result = VmSongParser.Parse(chart);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, result.errors.Count,
            "Expected exactly 1 error, got: " + string.Join(", ", result.errors.ConvertAll(e => e.message)));
        Assert.AreEqual("ContinueTest", result.chart.title);
        Assert.AreEqual(1, result.chart.tempoMap.segments.Count);
        Assert.AreEqual(1, result.chart.tracks.Count);
    }

    // 7. [Track] (인덱스 없음) → 에러
    [Test]
    public void TrackHeader_RequiresChannelIndex()
    {
        const string chart = @"
[Meta]
title=TrackTest

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track]
tick=0 note=60 len=240 vel=100
";
        var result = VmSongParser.Parse(chart);
        Assert.IsFalse(result.Success);

        bool hasIndexError = false;
        foreach (var err in result.errors)
            if (err.message.Contains("index") || err.message.Contains("Index"))
                hasIndexError = true;
        Assert.IsTrue(hasIndexError, "Expected an error about missing channel index");
    }

    // 8. 노트 tick 오름차순 정렬
    [Test]
    public void NotesSorted_ByTickAscending()
    {
        const string chart = @"
[Meta]
title=SortTest

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=0 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track:1]
tick=480 note=64 len=240 vel=100
tick=0 note=60 len=240 vel=100
tick=240 note=62 len=240 vel=100
";
        var result = VmSongParser.Parse(chart);
        Assert.IsTrue(result.Success,
            "Errors: " + string.Join(", ", result.errors.ConvertAll(e => e.message)));

        var notes = result.chart.tracks[0].notes;
        Assert.AreEqual(3,   notes.Count);
        Assert.AreEqual(0,   notes[0].tick);
        Assert.AreEqual(240, notes[1].tick);
        Assert.AreEqual(480, notes[2].tick);
    }

    // 9. 첫 tempo segment tick이 0이 아닐 때 에러
    [Test]
    public void TempoSegments_FirstMustStartAtZero()
    {
        const string chart = @"
[Meta]
title=BadTempo

[Resolution]
ticksPerQuarter=480

[Tempo]
tick=480 bpm=120 beats=4 beatUnit=4

[Channels]
channel=1 instrument=piano

[Track:1]
tick=480 note=60 len=240 vel=100
";
        var result = VmSongParser.Parse(chart);
        Assert.IsFalse(result.Success);

        bool hasFirstSegError = false;
        foreach (var err in result.errors)
            if (err.message.ToLowerInvariant().Contains("first") ||
                err.message.Contains("tick=0"))
                hasFirstSegError = true;
        Assert.IsTrue(hasFirstSegError, "Expected error about first segment starting at tick=0");
    }
}
