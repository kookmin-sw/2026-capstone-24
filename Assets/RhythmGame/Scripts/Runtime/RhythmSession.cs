using System;

public class RhythmSession
{
    public event Action OnNoteWindow;

    readonly InstrumentBase instrument;
    readonly RhythmSong     song;
    readonly IRhythmClock   clock;
    readonly RhythmJudge    judge;

    bool running;

    public RhythmSession(InstrumentBase instrument, RhythmSong song, IRhythmClock clock, RhythmJudge judge)
    {
        this.instrument = instrument;
        this.song       = song;
        this.clock      = clock;
        this.judge      = judge;
    }

    public double ElapsedTime => clock.CurrentTime;
    public bool   IsRunning   => running;

    public void Start()
    {
        running = true;
        instrument.MidiTriggered += OnMidiTriggered;
    }

    public void Stop()
    {
        running = false;
        instrument.MidiTriggered -= OnMidiTriggered;
    }

    void OnMidiTriggered(MidiEvent midiEvent)
    {
        judge.OnInput(midiEvent);
    }
}
