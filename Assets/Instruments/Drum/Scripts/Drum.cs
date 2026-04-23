using UnityEngine;

/// <summary>
/// 드럼 세트의 NoteOn/Off를 처리하고 고정 드럼 샘플을 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class Drum : InstrumentBase
{
    [Header("Piece Audio Settings")]
    [SerializeField, Min(1)] int maxVoices = 4;
    [SerializeField] bool spatialize = true;
    [SerializeField] bool spatializePostEffects = true;
    [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;
    [SerializeField] AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [SerializeField, Min(0f)] float minDistance = 0.8f;
    [SerializeField, Min(0f)] float maxDistance = 10f;
    [SerializeField, Min(0f)] float dopplerLevel;
    [SerializeField, Range(0f, 360f)] float spread;
    [SerializeField, Min(0f)] float reverbZoneMix = 1f;
    [SerializeField, Min(0f)] float releaseDuration = 0.05f;

    // 물리 타격 센서 등에서 호출하는 기존 API 유지
    public void Hit(int midiNote, float velocity) => TriggerMidi(new MidiEvent(midiNote, velocity, MidiEventType.NoteOn));
    public void Choke(int midiNote) => TriggerMidi(new MidiEvent(midiNote, 0f, MidiEventType.Choke));

    protected override InstrumentAudioOutput.AudioSourceSettings GetAudioSourceSettings()
    {
        return new InstrumentAudioOutput.AudioSourceSettings
        {
            MaxVoices = Mathf.Max(1, maxVoices),
            Spatialize = spatialize,
            SpatializePostEffects = spatializePostEffects,
            SpatialBlend = Mathf.Clamp01(spatialBlend),
            RolloffMode = rolloffMode,
            MinDistance = Mathf.Max(0f, minDistance),
            MaxDistance = Mathf.Max(minDistance, maxDistance),
            DopplerLevel = Mathf.Max(0f, dopplerLevel),
            Spread = Mathf.Clamp(spread, 0f, 360f),
            ReverbZoneMix = Mathf.Max(0f, reverbZoneMix),
            ReleaseDuration = Mathf.Max(0f, releaseDuration)
        };
    }

    protected override void OnChoke(MidiEvent midiEvent)
    {
        audioOutput.StopNoteImmediate(midiEvent.Note);
    }

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
