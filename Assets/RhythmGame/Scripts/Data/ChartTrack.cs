using System;
using System.Collections.Generic;

[Serializable]
public sealed class ChartTrack
{
    public int channel;
    public List<ChartNote> notes = new();
}
