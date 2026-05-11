using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 리듬게임 반주 재생기.
/// Begin() 호출 시 씬에 있는 모든 InstrumentBase를 자동으로 탐색해
/// chart.channelMap의 instrumentKey와 매칭한다.
/// judgedChannel을 제외한 모든 채널 이벤트를 해당 악기로 자동 재생하므로
/// 악기가 새로 추가돼도 별도 설정 없이 바로 반주에 포함된다.
/// </summary>
public class RhythmAccompaniment : MonoBehaviour
{
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

    /// <summary>
    /// 반주 세션을 시작한다.
    /// 씬의 InstrumentBase 목록을 chart.channelMap과 자동 매칭해 judgedChannel 외 채널을 재생한다.
    /// </summary>
    public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        End();
        _clock = clock;
        _map   = BuildInstrumentMap(chart, judgedChannel);

        _events = BuildEvents(chart, judgedChannel);
        _events.Sort((a, b) => a.fireTime.CompareTo(b.fireTime));
        _next    = 0;
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

    /// <summary>
    /// 씬에 있는 모든 InstrumentBase를 instrumentKey로 인덱싱한 뒤
    /// channelMap 에서 judgedChannel 을 제외한 채널에 매핑한다.
    /// </summary>
    Dictionary<int, InstrumentBase> BuildInstrumentMap(VmSongChart chart, int judgedChannel)
    {
        // 씬 내 모든 InstrumentBase를 InstrumentId → instance 로 수집
        var byKey = new Dictionary<string, InstrumentBase>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var inst in FindObjectsByType<InstrumentBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inst != null && !string.IsNullOrEmpty(inst.InstrumentId))
                byKey[inst.InstrumentId] = inst;
        }

        // channelMap 순회 → judgedChannel 제외한 채널에 악기 할당
        var map = new Dictionary<int, InstrumentBase>();
        foreach (var entry in chart.channelMap.entries)
        {
            if (entry.channel == judgedChannel) continue;
            if (byKey.TryGetValue(entry.instrumentKey, out var inst))
                map[entry.channel] = inst;
        }
        return map;
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
