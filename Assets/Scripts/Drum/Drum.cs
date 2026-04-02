using UnityEngine;

/// <summary>
/// 드럼 세트의 NoteOn/Off를 처리하고 고정 드럼 샘플을 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class Drum : InstrumentBase
{
    protected override string DefaultResourcePath => "Audio/Drum";

    // 물리 타격 센서 등에서 호출하는 기존 API 유지
    public void Hit(int midiNote, float velocity) => TriggerMidi(new MidiEvent(midiNote, velocity, true));
    public void Choke(int midiNote) => TriggerMidi(new MidiEvent(midiNote, 0f, false));

    protected override bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback)
    {
        playback = default;

        if (!TryGetAudioBank(out var bank))
            return false;

        if (!TryFindClipByPrefix(bank, midiEvent.Note.ToString(), out AudioClip clip))
            return false;

        playback = new NotePlayback(clip, 1f, midiEvent.Velocity);
        return true;
    }
}
