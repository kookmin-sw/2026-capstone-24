using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class RhythmJudgeTests
{
    class FakeTimeProvider : ITimeProvider
    {
        public double Now { get; set; }
    }

    // BPM=120, TPQ=480 → secondsPerTick = 1/960
    // 1.0s = 960 ticks, 0.95s = 912 ticks, 1.05s = 1008 ticks
    static VmSongChart MakeChart(float bpm = 120f, int ticksPerQuarter = 480)
    {
        var chart = new VmSongChart();
        chart.tempoMap = new TempoMap
        {
            ticksPerQuarter = ticksPerQuarter,
            segments = new List<TempoSegment>
            {
                new TempoSegment { tick = 0, bpm = bpm, beatsPerBar = 4, beatUnit = 4 }
            }
        };
        return chart;
    }

    static void AddNote(VmSongChart chart, int channel, byte midiNote, int tick)
    {
        ChartTrack track = null;
        foreach (var t in chart.tracks)
        {
            if (t.channel == channel) { track = t; break; }
        }
        if (track == null)
        {
            track = new ChartTrack { channel = channel };
            chart.tracks.Add(track);
        }
        track.notes.Add(new ChartNote { tick = tick, midiNote = midiNote, velocity = 100 });
    }

    // 1. 노트 1.0s, 입력 1.03s → Perfect
    [Test]
    public void InputWithinPerfectWindow_FiresPerfect()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Perfect, events[0].grade);
        Assert.AreEqual(60, events[0].midiNote);
    }

    // 2. 노트 1.0s, 입력 1.10s → Good
    [Test]
    public void InputWithinGoodWindow_FiresGood()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.10;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Good, events[0].grade);
    }

    // 3. 노트 1.0s, 입력 0.5s → 이벤트 없음. 1.20s Tick → Miss
    [Test]
    public void InputOutsideAllWindows_DoesNotMatch()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 0.5;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(0, events.Count);

        fake.Now = 1.20;
        judge.Tick();
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Miss, events[0].grade);
    }

    // 4. 노트 음 60, 입력 음 64 → 이벤트 없음. 1.20s Tick → Miss(60)
    [Test]
    public void InputWrongNote_DoesNotMatch()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(64, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(0, events.Count);

        fake.Now = 1.20;
        judge.Tick();
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Miss, events[0].grade);
        Assert.AreEqual(60, events[0].midiNote);
    }

    // 5. 입력 없이 1.20s Tick → Miss
    [Test]
    public void NotePassedWithoutInput_FiresMissOnTick()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.20;
        judge.Tick();

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Miss, events[0].grade);
    }

    // 6. 1.03s 입력 → Perfect. 이후 1.20s Tick → 추가 이벤트 없음
    [Test]
    public void InputMatched_NoMissLater()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(1, events.Count);

        fake.Now = 1.20;
        judge.Tick();
        Assert.AreEqual(1, events.Count);
    }

    // 7. 동시 노트 60/64/67. 1.03s에 60 입력 → Perfect(60). 1.20s Tick → Miss 두 건
    [Test]
    public void MultipleNotesAtSameTime_IndependentMatching()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        AddNote(chart, 1, 64, 960);
        AddNote(chart, 1, 67, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Perfect, events[0].grade);
        Assert.AreEqual(60, events[0].midiNote);

        fake.Now = 1.20;
        judge.Tick();
        Assert.AreEqual(3, events.Count);

        int missCount = 0;
        for (int i = 1; i < events.Count; i++)
            if (events[i].grade == JudgmentGrade.Miss) missCount++;
        Assert.AreEqual(2, missCount);
    }

    // 8. 채널 1과 10 노트. judgedChannel=1 → 채널 10 이벤트 없음
    [Test]
    public void OnlyJudgedChannel_IsTracked()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1,  60, 960);
        AddNote(chart, 10, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.20;
        judge.Tick();

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(1, events[0].channel);
    }

    // 9. 같은 노트에 1.03s, 1.04s 두 번 입력 → 첫 번만 매칭
    [Test]
    public void DuplicateInput_DoesNotDoubleMatch()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 960);
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(1, events.Count);

        fake.Now = 1.04;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));
        Assert.AreEqual(1, events.Count);
    }

    // 10. 음 60이 0.95s(912tick)와 1.05s(1008tick). 입력 1.03s → 1.05s 노트 Perfect. 1.20s Tick → 0.95s Miss
    [Test]
    public void NearestNoteMatched_WhenMultipleSameNoteInWindow()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        var chart = MakeChart();
        AddNote(chart, 1, 60, 912);   // 0.95s
        AddNote(chart, 1, 60, 1008);  // 1.05s
        clock.Start(chart);
        judge.Start(chart, 1);

        fake.Now = 1.03;
        judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn));

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JudgmentGrade.Perfect, events[0].grade);
        Assert.AreEqual(1.05, events[0].scheduledTime, 1e-9);

        fake.Now = 1.20;
        judge.Tick();

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(JudgmentGrade.Miss, events[1].grade);
        Assert.AreEqual(0.95, events[1].scheduledTime, 1e-9);
    }

    // 11. Start 전 OnInput → 이벤트 없음, 예외 없음
    [Test]
    public void OnInputBeforeStart_NoOp()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        Assert.DoesNotThrow(() =>
            judge.OnInput(new MidiEvent(60, 1f, MidiEventType.NoteOn)));
        Assert.AreEqual(0, events.Count);
    }

    // 12. Start 전 Tick → 이벤트 없음, 예외 없음
    [Test]
    public void TickBeforeStart_NoOp()
    {
        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var judge = new RhythmJudge(clock);
        var events = new List<JudgmentEvent>();
        judge.Judged += e => events.Add(e);

        Assert.DoesNotThrow(() => judge.Tick());
        Assert.AreEqual(0, events.Count);
    }
}
