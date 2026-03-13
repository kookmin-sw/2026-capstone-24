using UnityEngine;

public sealed class PianoMidiMapper
{
    public const int KeyCount = 88;
    public const int FirstMidiNote = 21;

    public PianoMidiEvent CreateNoteOn(int keyIndex, float velocity)
    {
        ValidateKeyIndex(keyIndex);
        return new PianoMidiEvent(FirstMidiNote + keyIndex, velocity, true);
    }

    public PianoMidiEvent CreateNoteOff(int keyIndex)
    {
        ValidateKeyIndex(keyIndex);
        return new PianoMidiEvent(FirstMidiNote + keyIndex, 0f, false);
    }

    static void ValidateKeyIndex(int keyIndex)
    {
        if (keyIndex < 0 || keyIndex >= KeyCount)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(keyIndex),
                keyIndex,
                $"Piano key index must be in range 0..{KeyCount - 1}.");
        }
    }
}
