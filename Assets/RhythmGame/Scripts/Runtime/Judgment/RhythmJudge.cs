using System;
using System.Collections.Generic;

public class RhythmJudge
{
    const double PERFECT_WINDOW_SEC = 0.050;
    const double GOOD_WINDOW_SEC    = 0.150;

    struct PendingNote
    {
        public int    channel;
        public byte   midiNote;
        public int    tick;
        public double scheduledTime;
        public bool   judged;
    }

    readonly IRhythmClock      _clock;
    readonly List<PendingNote> _pending = new();
    int  _nextMissCheckIndex;
    bool _running;

    public RhythmJudge(IRhythmClock clock)
    {
        _clock = clock;
    }

    public event Action<JudgmentEvent> Judged;

    public void Start(VmSongChart chart, int judgedChannel)
    {
        _pending.Clear();
        _nextMissCheckIndex = 0;

        foreach (var track in chart.tracks)
        {
            if (track.channel != judgedChannel) continue;
            foreach (var note in track.notes)
            {
                _pending.Add(new PendingNote
                {
                    channel       = track.channel,
                    midiNote      = note.midiNote,
                    tick          = note.tick,
                    scheduledTime = chart.tempoMap.TickToSeconds(note.tick),
                    judged        = false
                });
            }
        }

        _pending.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));
        _running = true;
    }

    public void Stop()
    {
        _running = false;
    }

    public void OnInput(MidiEvent midiEvent)
    {
        if (!_running) return;

        double t         = _clock.CurrentTime;
        int    bestIndex = -1;
        double bestDiff  = double.MaxValue;

        for (int i = 0; i < _pending.Count; i++)
        {
            var p = _pending[i];
            if (p.judged) continue;
            if (midiEvent.Note != p.midiNote) continue;
            double diff = Math.Abs(p.scheduledTime - t);
            if (diff > GOOD_WINDOW_SEC) continue;
            if (diff < bestDiff)
            {
                bestDiff  = diff;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return;

        var matched = _pending[bestIndex];
        matched.judged      = true;
        _pending[bestIndex] = matched;

        var grade = bestDiff <= PERFECT_WINDOW_SEC ? JudgmentGrade.Perfect : JudgmentGrade.Good;
        Judged?.Invoke(new JudgmentEvent(matched.channel, matched.midiNote, matched.tick,
                                         matched.scheduledTime, t, grade));
    }

    public void Tick()
    {
        if (!_running) return;

        double now = _clock.CurrentTime;

        while (_nextMissCheckIndex < _pending.Count)
        {
            var p = _pending[_nextMissCheckIndex];
            if (p.judged)
            {
                _nextMissCheckIndex++;
                continue;
            }
            if (now > p.scheduledTime + GOOD_WINDOW_SEC)
            {
                p.judged                      = true;
                _pending[_nextMissCheckIndex] = p;
                _nextMissCheckIndex++;
                Judged?.Invoke(new JudgmentEvent(p.channel, p.midiNote, p.tick,
                                                  p.scheduledTime, double.NaN, JudgmentGrade.Miss));
            }
            else
            {
                break;
            }
        }
    }
}
