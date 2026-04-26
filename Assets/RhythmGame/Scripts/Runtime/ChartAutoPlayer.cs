using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChartAutoPlayer : MonoBehaviour
{
    [SerializeField] string songRelativePath = "Songs/test.vmsong";
    [SerializeField] ChannelBinding[] channelBindings = Array.Empty<ChannelBinding>();

    [Serializable]
    public struct ChannelBinding
    {
        [Tooltip("1~16 (.vmsong 채널 번호와 일치)")]
        public int channel;
        public InstrumentBase instrument;
    }

    readonly struct ScheduledEvent
    {
        public readonly float         fireTime;
        public readonly int           midiNote;
        public readonly float         velocity;
        public readonly MidiEventType type;
        public readonly int           channel;

        public ScheduledEvent(float t, int n, float v, MidiEventType et, int ch)
        { fireTime = t; midiNote = n; velocity = v; type = et; channel = ch; }
    }

    List<ScheduledEvent>            _events;
    Dictionary<int, InstrumentBase> _map;
    int   _next;
    float _elapsed;
    bool  _playing;

    void Start()
    {
        _map = new Dictionary<int, InstrumentBase>();
        foreach (var b in channelBindings)
            if (b.instrument != null) _map[b.channel] = b.instrument;

        if (_map.Count == 0)
        {
            Debug.LogError("[ChartAutoPlayer] Channel Bindings가 비어 있습니다.", this);
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, songRelativePath);
        if (!File.Exists(path))
        {
            Debug.LogError($"[ChartAutoPlayer] 파일 없음: {path}", this);
            return;
        }

        var result = VmSongParser.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
        if (!result.Success)
        {
            foreach (var e in result.errors)
                Debug.LogError($"[ChartAutoPlayer] 파싱 오류 line {e.line}: {e.message}", this);
            return;
        }

        _events = BuildEvents(result.chart);
        _events.Sort((a, b) => a.fireTime.CompareTo(b.fireTime));
        Debug.Log($"[ChartAutoPlayer] '{result.chart.title}' 이벤트 {_events.Count}개. 재생 시작.", this);

        _elapsed = 0f;
        _next    = 0;
        _playing = true;
    }

    void Update()
    {
        if (!_playing) return;
        _elapsed += Time.deltaTime;
        while (_next < _events.Count && _events[_next].fireTime <= _elapsed)
            Fire(_events[_next++]);
        if (_next >= _events.Count)
        {
            _playing = false;
            Debug.Log("[ChartAutoPlayer] 재생 완료.", this);
        }
    }

    static List<ScheduledEvent> BuildEvents(VmSongChart chart)
    {
        var list = new List<ScheduledEvent>();
        foreach (var track in chart.tracks)
            foreach (var note in track.notes)
            {
                float on  = (float)chart.tempoMap.TickToSeconds(note.tick);
                float off = (float)chart.tempoMap.TickToSeconds(note.tick + note.durationTicks);
                float vel = note.velocity / 127f;
                list.Add(new ScheduledEvent(on,  note.midiNote, vel, MidiEventType.NoteOn,  track.channel));
                list.Add(new ScheduledEvent(off, note.midiNote, 0f, MidiEventType.NoteOff, track.channel));
            }
        return list;
    }

    void Fire(ScheduledEvent ev)
    {
        if (!_map.TryGetValue(ev.channel, out var inst)) return;
        inst.TriggerMidi(new MidiEvent(ev.midiNote, ev.velocity, ev.type, (byte)(ev.channel - 1)));
    }
}
