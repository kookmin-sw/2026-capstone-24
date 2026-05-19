using System.Collections.Generic;
using NUnit.Framework;
using RhythmGame.Data;
using RhythmGame.Runtime;
using RhythmGame.Runtime.Clock;
using UnityEngine;

[TestFixture]
public class RhythmAccompanimentTests
{
    class FakeTimeProvider : ITimeProvider
    {
        public double Now { get; set; }
    }

    static VmSongChart MakeMinimalChart()
    {
        var chart = new VmSongChart();
        chart.tempoMap = new TempoMap
        {
            ticksPerQuarter = 480,
            segments = new List<TempoSegment>
            {
                new TempoSegment { tick = 0, bpm = 120f, beatsPerBar = 4, beatUnit = 4 }
            }
        };
        chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry { channel = 1, instrumentKey = "piano" });
        chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry { channel = 2, instrumentKey = "drum" });
        return chart;
    }

    [Test]
    public void ShouldFire_RespectsEnabledDictionary_OffChannelSkipped()
    {
        var go  = new GameObject("AccompanimentTest");
        var acc = go.AddComponent<RhythmAccompaniment>();

        var fake  = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var chart = MakeMinimalChart();

        // judgedChannel=1; enabled sdict: channel 2 is OFF
        acc.Begin(chart, 1, clock, new Dictionary<int, bool> { { 2, false } });

        // channel 2 is explicitly OFF → must not fire
        Assert.IsFalse(acc.ShouldFire(2), "channel 2 must be OFF per enabled dict");

        // channel 1 (judgedChannel) is not in dict → fallback true
        Assert.IsTrue(acc.ShouldFire(1), "channel 1 not in dict → fallback true");

        // channel 99 (unknown) is not in dict → fallback true
        Assert.IsTrue(acc.ShouldFire(99), "unknown channel → fallback true");

        // --- second case: null enabled → all ON (backward compat) ---
        acc.Begin(chart, 1, clock);

        Assert.IsTrue(acc.ShouldFire(2), "null enabled → channel 2 should fire (backward compat)");

        Object.DestroyImmediate(go);
    }
}
