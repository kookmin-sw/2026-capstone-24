using System;

[Serializable]
public struct TempoSegment
{
    public int   tick;
    public float bpm;
    public int   beatsPerBar;
    public int   beatUnit;
}
