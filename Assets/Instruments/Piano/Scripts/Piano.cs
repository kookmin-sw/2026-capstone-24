using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피아노 악기의 MIDI 입력을 처리하고 피아노 샘플을 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class Piano : InstrumentBase
{
    const int KeyCount = 88;
    const int FirstMidiNote = 21;

    // 물리 건반 센서 등에서 호출하는 기존 API 유지
    public void NoteOn(int keyIndex, float velocity)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, velocity, true));
    }

    public void NoteOff(int keyIndex)
    {
        if (!IsValidKeyIndex(keyIndex))
            return;

        TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, 0f, false));
    }

    protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
    {
        playback = default;

        if (!TryGetAudioBank(out Dictionary<string, AudioClip> bank))
            return false;

        if (!TryResolveMelodicPlayback(midiEvent.Note, bank, out AudioClip clip, out float pitch))
            return false;

        playback = new NotePlayback(clip, pitch, midiEvent.Velocity);
        return true;
    }

    bool TryResolveMelodicPlayback(int midiNote, Dictionary<string, AudioClip> bank, out AudioClip clip, out float pitch)
    {
        clip = null;
        pitch = 1f;

        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;

        string targetBase;
        int sourceIndex;

        if (noteIndex >= 1 && noteIndex <= 6)
        {
            targetBase = "Ds" + octave;
            sourceIndex = 3;
        }
        else
        {
            int targetOctave = noteIndex <= 1 ? octave - 1 : octave;
            targetBase = "A" + targetOctave;
            sourceIndex = 9;
        }

        if (!bank.TryGetValue(targetBase, out clip))
            return false;

        int diff = noteIndex - sourceIndex;
        if (noteIndex <= 1 && sourceIndex == 9)
            diff += 12;

        pitch = Mathf.Pow(1.059463f, diff);
        return true;
    }

    bool IsValidKeyIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < KeyCount)
            return true;

        Debug.LogWarning($"[Piano] 유효하지 않은 건반 인덱스가 전달되었습니다: {keyIndex}", this);
        return false;
    }
}
