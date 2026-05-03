using System;
using System.Collections.Generic;
using UnityEngine;

public class RhythmAccompaniment : MonoBehaviour
{
    [Serializable]
    public struct ChannelBinding
    {
        [Tooltip("1~16 (.vmsong 채널 번호)")]
        public int channel;
        public InstrumentBase instrument;
    }

    [SerializeField] ChannelBinding[] channelBindings = Array.Empty<ChannelBinding>();

    readonly struct ScheduledEvent
    {
        public readonly double        fireTime;
        public readonly int           midiNote;
        public readonly float         velocity;
        public readonly MidiEventType type;
        public readonly int           channel;

        public ScheduledEvent(double t, int n, float v, MidiEventType et, int ch)
        { fireTime = t; midiNote = n; velocity = v; type = et; channel = ch; }
    }

    List<ScheduledEvent>            _events;
    Dictionary<int, InstrumentBase> _map;
    IRhythmClock                    _clock;
    int                             _next;
    bool                            _playing;

    public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        End();
        _clock = clock;
        _map   = new Dictionary<int, InstrumentBase>();
        foreach (var b in channelBindings)
            if (b.instrument != null && b.channel != judgedChannel)
                _map[b.channel] = b.instrument;

        _events = BuildEvents(chart, judgedChannel);
        _events.Sort((a, b) => a.fireTime.CompareTo(b.fireTime));
        _next   = 0;
        _playing = true;
    }

    public void End()
    {
        _playing = false;
        _clock   = null;
        _events  = null;
        _next    = 0;
    }

    void Update()
    {
        if (!_playing || _clock == null) return;
        if (_clock.State != RhythmClockState.Running) return;

        double now = _clock.CurrentTime;
        while (_next < _events.Count && _events[_next].fireTime <= now)
            Fire(_events[_next++]);

        if (_next >= _events.Count)
            _playing = false;
    }

    static List<ScheduledEvent> BuildEvents(VmSongChart chart, int judgedChannel)
    {
        var list = new List<ScheduledEvent>();
        foreach (var track in chart.tracks)
        {
            if (track.channel == judgedChannel) continue;
            foreach (var note in track.notes)
            {
                double on  = chart.tempoMap.TickToSeconds(note.tick);
                double off = chart.tempoMap.TickToSeconds(note.tick + note.durationTicks);
                float  vel = note.velocity / 127f;
                list.Add(new ScheduledEvent(on,  note.midiNote, vel, MidiEventType.NoteOn,  track.channel));
                list.Add(new ScheduledEvent(off, note.midiNote, 0f, MidiEventType.NoteOff, track.channel));
            }
        }
        return list;
    }

    void Fire(ScheduledEvent ev)
    {
        if (!_map.TryGetValue(ev.channel, out var inst)) return;
        inst.TriggerMidi(new MidiEvent(ev.midiNote, ev.velocity, ev.type, (byte)(ev.channel - 1)));
    }
}
