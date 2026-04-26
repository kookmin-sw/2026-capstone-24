using System;

public sealed partial class TempoMap
{
    // segments는 tick 오름차순이 invariant (파서가 보장). targetTick < 0은 0으로 clamp.
    public double TickToSeconds(int targetTick)
    {
        if (targetTick < 0) targetTick = 0;
        if (segments.Count == 0 || ticksPerQuarter <= 0) return 0.0;

        double accumulated = 0.0;

        for (int i = 0; i < segments.Count; i++)
        {
            int segStart = segments[i].tick;
            int segEnd   = (i + 1 < segments.Count) ? segments[i + 1].tick : int.MaxValue;

            if (targetTick <= segStart) break;

            int    ticksInSeg     = Math.Min(targetTick, segEnd) - segStart;
            double secondsPerTick = 60.0 / ((double)segments[i].bpm * ticksPerQuarter);
            accumulated += ticksInSeg * secondsPerTick;

            if (targetTick < segEnd) break;
        }

        return accumulated;
    }
}
