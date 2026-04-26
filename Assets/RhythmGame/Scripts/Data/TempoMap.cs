using System;
using System.Collections.Generic;

[Serializable]
public sealed partial class TempoMap
{
    public int ticksPerQuarter;
    public List<TempoSegment> segments = new();
}
