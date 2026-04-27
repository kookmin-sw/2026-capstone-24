using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class RhythmClockTests
{
    class FakeTimeProvider : ITimeProvider
    {
        public double Now { get; set; }
    }

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

    // 1. Start 직후 state==Running, CurrentTime==0
    [Test]
    public void Start_PutsStateRunning_AndCurrentTimeBeginsAtZero()
    {
        var fake = new FakeTimeProvider { Now = 100.0 };
        var clock = new RhythmClock(fake);

        clock.Start(MakeChart());

        Assert.AreEqual(RhythmClockState.Running, clock.State);
        Assert.AreEqual(0.0, clock.CurrentTime, 1e-9);
    }

    // 2. provider.Now를 +1.5초 진행시키면 CurrentTime ≈ 1.5
    [Test]
    public void CurrentTime_AdvancesWithTimeProvider()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());

        fake.Now = 1.5;

        Assert.AreEqual(1.5, clock.CurrentTime, 1e-9);
    }

    // 3. Running 중 +0.5초 후 Pause — 이후 provider 추가로 +1초 진행해도 CurrentTime은 0.5
    [Test]
    public void Pause_FreezesCurrentTime()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());

        fake.Now = 0.5;
        clock.Pause();

        fake.Now = 1.5;

        Assert.AreEqual(0.5, clock.CurrentTime, 1e-9);
        Assert.AreEqual(RhythmClockState.Paused, clock.State);
    }

    // 4. Pause 시나리오 이어서 Resume 후 +0.3초 → CurrentTime ≈ 0.8
    [Test]
    public void Resume_ContinuesFromPausedTime()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());

        fake.Now = 0.5;
        clock.Pause();

        fake.Now = 1.5;
        clock.Resume();

        fake.Now = 1.8;

        Assert.AreEqual(0.8, clock.CurrentTime, 1e-9);
        Assert.AreEqual(RhythmClockState.Running, clock.State);
    }

    // 5. Running/Paused에서 Stop 후 CurrentTime==0, state==Stopped
    [Test]
    public void Stop_ResetsCurrentTimeToZero_FromRunning()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());
        fake.Now = 2.0;

        clock.Stop();

        Assert.AreEqual(RhythmClockState.Stopped, clock.State);
        Assert.AreEqual(0.0, clock.CurrentTime, 1e-9);
    }

    [Test]
    public void Stop_ResetsCurrentTimeToZero_FromPaused()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());
        fake.Now = 1.0;
        clock.Pause();

        clock.Stop();

        Assert.AreEqual(RhythmClockState.Stopped, clock.State);
        Assert.AreEqual(0.0, clock.CurrentTime, 1e-9);
    }

    // 6. Stop 후 다시 Start하면 CurrentTime==0에서 재시작
    [Test]
    public void Restart_AfterStop_BeginsAtZero()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());
        fake.Now = 5.0;
        clock.Stop();

        fake.Now = 10.0;
        clock.Start(MakeChart());

        Assert.AreEqual(0.0, clock.CurrentTime, 1e-9);
        Assert.AreEqual(RhythmClockState.Running, clock.State);
    }

    // 7. TickToSeconds가 chart TempoMap 값과 동일
    [Test]
    public void TickToSeconds_DelegatesToTempoMap()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        var chart = MakeChart(bpm: 120f, ticksPerQuarter: 480);
        clock.Start(chart);

        // bpm=120, tpq=480 → secondsPerTick = 60/(120*480) = 1/960
        // tick=480 → 0.5초
        double expected = chart.tempoMap.TickToSeconds(480);
        Assert.AreEqual(expected, clock.TickToSeconds(480), 1e-9);
    }

    // 8. Idle 상태에서 Pause 호출 → state는 Idle 유지
    [Test]
    public void PauseFromIdle_NoOp()
    {
        var clock = new RhythmClock(new FakeTimeProvider { Now = 0.0 });

        clock.Pause();

        Assert.AreEqual(RhythmClockState.Idle, clock.State);
    }

    // 9. Running 상태에서 Resume 호출 → 상태/시간 변동 없음
    [Test]
    public void ResumeFromRunning_NoOp()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);
        clock.Start(MakeChart());
        fake.Now = 1.0;

        clock.Resume();

        Assert.AreEqual(RhythmClockState.Running, clock.State);
        Assert.AreEqual(1.0, clock.CurrentTime, 1e-9);
    }

    // 10. 같은 상태로의 전이는 StateChanged를 발화하지 않음
    [Test]
    public void StateChanged_FiresOnlyOnActualTransition()
    {
        var fake = new FakeTimeProvider { Now = 0.0 };
        var clock = new RhythmClock(fake);

        int fireCount = 0;
        clock.StateChanged += _ => fireCount++;

        clock.Start(MakeChart());   // Idle→Running: +1
        clock.Resume();             // Running→Running: no-op, +0
        clock.Pause();              // Running→Paused: +1
        clock.Pause();              // Paused→Paused: no-op, +0
        clock.Stop();               // Paused→Stopped: +1

        Assert.AreEqual(3, fireCount);
    }
}
