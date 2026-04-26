using System;
using System.Collections.Generic;
using System.Globalization;

public static class VmSongParser
{
    public sealed class ParseError
    {
        public int    line;
        public string message;
    }

    public sealed class ParseResult
    {
        public VmSongChart     chart  = new();
        public List<ParseError> errors = new();
        public bool Success => errors.Count == 0;
    }

    public static ParseResult Parse(string text)
    {
        var result = new ParseResult();

        if (string.IsNullOrEmpty(text))
        {
            result.errors.Add(new ParseError { line = 0, message = "Empty input." });
            return result;
        }

        string currentSection = null;
        int    currentChannel = -1;

        bool hasMeta       = false;
        bool hasResolution = false;
        bool hasTempo      = false;
        bool hasChannels   = false;
        bool hasTrack      = false;

        int  lastBeats = 4;
        int  lastUnit  = 4;
        bool firstTempo = true;

        string[] lines = text.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            int    lineNum = li + 1;
            string raw     = lines[li];

            if (raw.Length > 0 && raw[raw.Length - 1] == '\r')
                raw = raw.Substring(0, raw.Length - 1);

            int hash = raw.IndexOf('#');
            if (hash >= 0) raw = raw.Substring(0, hash);

            raw = raw.Trim();
            if (raw.Length == 0) continue;

            // ── Section header ─────────────────────────────────────────────
            if (raw[0] == '[' && raw[raw.Length - 1] == ']')
            {
                string inner = raw.Substring(1, raw.Length - 2).Trim();
                int    colon = inner.IndexOf(':');
                string sName = (colon >= 0 ? inner.Substring(0, colon) : inner).Trim().ToLowerInvariant();

                currentSection = null;
                currentChannel = -1;

                switch (sName)
                {
                    case "meta":
                        currentSection = "meta";
                        hasMeta        = true;
                        break;
                    case "resolution":
                        currentSection = "resolution";
                        hasResolution  = true;
                        break;
                    case "tempo":
                        currentSection = "tempo";
                        hasTempo       = true;
                        break;
                    case "channels":
                        currentSection = "channels";
                        hasChannels    = true;
                        break;
                    case "track":
                        if (colon < 0)
                        {
                            result.errors.Add(new ParseError { line = lineNum,
                                message = "[Track] requires a channel index, e.g. [Track:1]." });
                        }
                        else
                        {
                            string idxStr = inner.Substring(colon + 1).Trim();
                            if (!int.TryParse(idxStr, out int ch) ||
                                ch < RhythmChannels.MinChannel || ch > RhythmChannels.MaxChannel)
                            {
                                result.errors.Add(new ParseError { line = lineNum,
                                    message = $"[Track:{idxStr}] channel must be an integer 1..16." });
                            }
                            else
                            {
                                currentSection = "track";
                                currentChannel = ch;
                                hasTrack       = true;
                            }
                        }
                        break;
                    default:
                        result.errors.Add(new ParseError { line = lineNum,
                            message = $"Unknown section '[{inner}]'." });
                        break;
                }
                continue;
            }

            if (currentSection == null) continue;

            var kv = ParseKV(raw);

            switch (currentSection)
            {
                case "meta":
                    HandleMeta(kv, result.chart);
                    break;
                case "resolution":
                    HandleResolution(kv, result.chart, lineNum, result.errors);
                    break;
                case "tempo":
                    HandleTempo(kv, result.chart, lineNum, result.errors,
                                ref lastBeats, ref lastUnit, ref firstTempo);
                    break;
                case "channels":
                    HandleChannel(kv, result.chart, lineNum, result.errors);
                    break;
                case "track":
                    HandleNote(kv, currentChannel, result.chart, lineNum, result.errors);
                    break;
            }
        }

        // ── Post-parse validation ───────────────────────────────────────────
        if (!hasMeta)       result.errors.Add(new ParseError { line = 0, message = "Missing required section [Meta]." });
        if (!hasResolution) result.errors.Add(new ParseError { line = 0, message = "Missing required section [Resolution]." });
        if (!hasTempo)      result.errors.Add(new ParseError { line = 0, message = "Missing required section [Tempo]." });
        if (!hasChannels)   result.errors.Add(new ParseError { line = 0, message = "Missing required section [Channels]." });
        if (!hasTrack)      result.errors.Add(new ParseError { line = 0, message = "Missing required [Track:N] section." });

        result.chart.tempoMap.segments.Sort((a, b) => a.tick.CompareTo(b.tick));

        if (result.chart.tempoMap.segments.Count > 0 &&
            result.chart.tempoMap.segments[0].tick != 0)
        {
            result.errors.Add(new ParseError { line = 0,
                message = "First [Tempo] segment must start at tick=0." });
        }

        foreach (var track in result.chart.tracks)
            StableInsertionSort(track.notes);

        return result;
    }

    // ── Key-value parsing ───────────────────────────────────────────────────

    static List<(string, string)> ParseKV(string line)
    {
        var pairs = new List<(string, string)>();
        int i = 0, n = line.Length;

        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(line[i])) i++;
            if (i >= n) break;

            int ks = i;
            while (i < n && line[i] != '=' && !char.IsWhiteSpace(line[i])) i++;
            string key = line.Substring(ks, i - ks).ToLowerInvariant();

            while (i < n && char.IsWhiteSpace(line[i])) i++;
            if (i >= n || line[i] != '=') continue;
            i++;

            while (i < n && char.IsWhiteSpace(line[i])) i++;
            int vs = i;
            while (i < n && !char.IsWhiteSpace(line[i])) i++;
            string val = line.Substring(vs, i - vs);

            if (key.Length > 0 && val.Length > 0)
                pairs.Add((key, val));
        }
        return pairs;
    }

    static bool TryGet(List<(string, string)> kv, string key, out string val)
    {
        foreach (var (k, v) in kv)
            if (k == key) { val = v; return true; }
        val = null;
        return false;
    }

    static bool TryGetInt(List<(string, string)> kv, string key, out int value)
    {
        if (TryGet(kv, key, out string s) && int.TryParse(s, out value)) return true;
        value = 0;
        return false;
    }

    // ── Section handlers ────────────────────────────────────────────────────

    static void HandleMeta(List<(string, string)> kv, VmSongChart chart)
    {
        if (TryGet(kv, "title",  out string t))  chart.title  = t;
        if (TryGet(kv, "artist", out string ar)) chart.artist = ar;
        if (TryGet(kv, "songid", out string si)) chart.songId = si;
    }

    static void HandleResolution(List<(string, string)> kv, VmSongChart chart, int line, List<ParseError> errors)
    {
        if (!TryGetInt(kv, "ticksperquarter", out int tpq) || tpq <= 0)
        {
            errors.Add(new ParseError { line = line,
                message = "Resolution: ticksPerQuarter must be a positive integer." });
            return;
        }
        chart.tempoMap.ticksPerQuarter = tpq;
    }

    static void HandleTempo(List<(string, string)> kv, VmSongChart chart, int line, List<ParseError> errors,
                            ref int lastBeats, ref int lastUnit, ref bool first)
    {
        if (!TryGetInt(kv, "tick", out int tick))
        {
            errors.Add(new ParseError { line = line, message = "Tempo: missing or invalid 'tick'." });
            return;
        }

        if (!TryGet(kv, "bpm", out string bpmStr) ||
            !float.TryParse(bpmStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm) ||
            bpm <= 0f)
        {
            errors.Add(new ParseError { line = line, message = "Tempo: missing or invalid 'bpm'." });
            return;
        }

        int beats = first ? 4 : lastBeats;
        int unit  = first ? 4 : lastUnit;

        if (TryGetInt(kv, "beats", out int b) && b > 0)    beats = b;
        if (TryGetInt(kv, "beatunit", out int u) && u > 0) unit  = u;

        lastBeats = beats;
        lastUnit  = unit;
        first     = false;

        chart.tempoMap.segments.Add(new TempoSegment
        {
            tick        = tick,
            bpm         = bpm,
            beatsPerBar = beats,
            beatUnit    = unit,
        });
    }

    static void HandleChannel(List<(string, string)> kv, VmSongChart chart, int line, List<ParseError> errors)
    {
        if (!TryGetInt(kv, "channel", out int ch) ||
            ch < RhythmChannels.MinChannel || ch > RhythmChannels.MaxChannel)
        {
            errors.Add(new ParseError { line = line,
                message = "Channels: 'channel' must be an integer 1..16." });
            return;
        }
        if (!TryGet(kv, "instrument", out string instr))
        {
            errors.Add(new ParseError { line = line, message = "Channels: missing 'instrument'." });
            return;
        }
        chart.channelMap.entries.Add(new ChannelInstrumentMap.Entry
        {
            channel       = ch,
            instrumentKey = instr,
        });
    }

    static void HandleNote(List<(string, string)> kv, int channel, VmSongChart chart, int line, List<ParseError> errors)
    {
        if (!TryGetInt(kv, "tick",  out int tick)  || tick < 0          ||
            !TryGetInt(kv, "note",  out int note)  || (uint)note > 127  ||
            !TryGetInt(kv, "len",   out int len)   || len < 1           ||
            !TryGetInt(kv, "vel",   out int vel)   || (uint)vel  > 127)
        {
            errors.Add(new ParseError { line = line,
                message = $"Track {channel}: malformed note line." });
            return;
        }

        var track = FindOrCreateTrack(chart, channel);
        track.notes.Add(new ChartNote
        {
            tick          = tick,
            midiNote      = (byte)note,
            durationTicks = len,
            velocity      = (byte)vel,
        });
    }

    static ChartTrack FindOrCreateTrack(VmSongChart chart, int channel)
    {
        foreach (var t in chart.tracks)
            if (t.channel == channel) return t;
        var track = new ChartTrack { channel = channel };
        chart.tracks.Add(track);
        return track;
    }

    // ── Stable insertion sort (equal ticks preserve input order) ───────────

    static void StableInsertionSort(List<ChartNote> notes)
    {
        for (int i = 1; i < notes.Count; i++)
        {
            var key = notes[i];
            int j   = i - 1;
            while (j >= 0 && notes[j].tick > key.tick)
            {
                notes[j + 1] = notes[j];
                j--;
            }
            notes[j + 1] = key;
        }
    }
}
