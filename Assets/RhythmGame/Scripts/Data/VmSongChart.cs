using System;
using System.Collections.Generic;

namespace RhythmGame.Data
{
    [Serializable]
    public sealed class VmSongChart
    {
        public string title;
        public string artist;
        public string songId;
        public TempoMap           tempoMap   = new();
        public ChannelInstrumentMap channelMap = new();
        public List<ChartTrack>   tracks     = new();
    }
}
