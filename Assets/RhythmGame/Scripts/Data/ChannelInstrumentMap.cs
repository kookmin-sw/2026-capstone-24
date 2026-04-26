using System;
using System.Collections.Generic;

[Serializable]
public sealed class ChannelInstrumentMap
{
    [Serializable]
    public struct Entry
    {
        public int    channel;
        public string instrumentKey;
    }

    public List<Entry> entries = new();
}
