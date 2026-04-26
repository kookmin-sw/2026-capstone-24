using System;
using UnityEngine;

public class RhythmSession
{
    public event Action OnNoteWindow;

    readonly InstrumentBase instrument;
    readonly RhythmSong song;

    float elapsedTime;
    bool running;

    public RhythmSession(InstrumentBase instrument, RhythmSong song)
    {
        this.instrument = instrument;
        this.song = song;
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
