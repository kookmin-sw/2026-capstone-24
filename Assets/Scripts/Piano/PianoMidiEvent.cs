using UnityEngine;

public readonly struct PianoMidiEvent
{
    public PianoMidiEvent(int note, float velocity, bool isNoteOn)
    {
        Note = note;
        Velocity = Mathf.Clamp01(velocity);
        IsNoteOn = isNoteOn;
    }

    public int Note { get; }
    public float Velocity { get; }
    public bool IsNoteOn { get; }
}
