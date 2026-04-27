using System;

[Serializable]
public struct ChartNote
{
    public int  tick;
    public byte midiNote;
    public int  durationTicks;
    public byte velocity;
}
