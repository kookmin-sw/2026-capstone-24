using System;
using System.Collections.Generic;

namespace RhythmGame.Data
{
    [Serializable]
    public sealed partial class TempoMap
    {
        public int ticksPerQuarter;
        public List<TempoSegment> segments = new();
    }
}
