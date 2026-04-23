using System;
using UnityEngine;

public class RhythmSession
{
    public event Action<RhythmNote> OnNoteWindow;

    readonly InstrumentBase instrument;
    readonly RhythmSong song;
    readonly RhythmDifficulty difficulty;

    float elapsedTime;
    bool running;

    public RhythmSession(InstrumentBase instrument, RhythmSong song, RhythmDifficulty difficulty)
    {
        this.instrument = instrument;
        this.song = song;
        this.difficulty = difficulty;
    }

    public float ElapsedTime => elapsedTime;
    public bool IsRunning => running;

    public void Start()
    {
        elapsedTime = 0f;
        running = true;
        instrument.MidiTriggered += OnMidiTriggered;
    }

    public void Tick(float deltaTime)
    {
        if (!running)
            return;

        elapsedTime += deltaTime;
    }

    public void Stop()
    {
        running = false;
        instrument.MidiTriggered -= OnMidiTriggered;
    }

    void OnMidiTriggered(MidiEvent midiEvent)
    {
        Debug.Log($"[RhythmSession] MidiTriggered note={midiEvent.Note} velocity={midiEvent.Velocity:F2} t={elapsedTime:F3}s");
    }
}
