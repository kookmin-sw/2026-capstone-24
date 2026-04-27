using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class TempoMapResolverTests
{
    const double Epsilon = 1e-6;

    static TempoMap SingleSegment(float bpm, int tpq) => new TempoMap
    {
        ticksPerQuarter = tpq,
        segments = new List<TempoSegment>
        {
            new TempoSegment { tick = 0, bpm = bpm, beatsPerBar = 4, beatUnit = 4 }
        }
    };

    // 1. 단일 BPM=120, TPQ=480: 480 tick → 0.5초
    [Test]
    public void SingleSegment_120Bpm_480Tpq()
    {
        var map    = SingleSegment(120f, 480);
        double sec = map.TickToSeconds(480);
        Assert.AreEqual(0.5, sec, Epsilon);
    }

    // 2. 다중 세그먼트 누적: seg0=120bpm@0, seg1=60bpm@480 → tick=960 → 1.5초
    [Test]
    public void MultiSegment_AccumulatesCorrectly()
    {
        var map = new TempoMap
        {
            ticksPerQuarter = 480,
            segments = new List<TempoSegment>
            {
                new TempoSegment { tick =   0, bpm = 120f, beatsPerBar = 4, beatUnit = 4 },
                new TempoSegment { tick = 480, bpm =  60f, beatsPerBar = 4, beatUnit = 4 },
            }
        };
        // 480 ticks @ 120bpm = 0.5s, 480 ticks @ 60bpm = 1.0s → total 1.5s
        double sec = map.TickToSeconds(960);
        Assert.AreEqual(1.5, sec, Epsilon);
    }

    // 3. segment 경계 tick 질의 → 직전 누적만 반환 (신규 segment 기여 0)
    [Test]
    public void TickAtSegmentBoundary_UsesPriorSegment()
    {
        var map = new TempoMap
        {
            ticksPerQuarter = 480,
            segments = new List<TempoSegment>
            {
                new TempoSegment { tick =   0, bpm = 120f, beatsPerBar = 4, beatUnit = 4 },
                new TempoSegment { tick = 480, bpm =  60f, beatsPerBar = 4, beatUnit = 4 },
            }
        };
        // 딱 480 tick → 0.5s (60bpm segment 기여 0)
        double sec = map.TickToSeconds(480);
        Assert.AreEqual(0.5, sec, Epsilon);
    }

    // 4. 음수 tick → 0초
    [Test]
    public void NegativeTick_ClampsToZero()
    {
        var map = SingleSegment(120f, 480);
        Assert.AreEqual(0.0, map.TickToSeconds(-1), Epsilon);
    }

    // 5. segments 비어 있을 때 → 0초
    [Test]
    public void EmptyTempoMap_ReturnsZero()
    {
        var map = new TempoMap { ticksPerQuarter = 480 };
        Assert.AreEqual(0.0, map.TickToSeconds(480), Epsilon);
    }
}
