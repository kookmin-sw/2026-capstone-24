using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 악기의 공통 속성과 초기화 로직을 관리하는 베이스 클래스입니다.
/// </summary>
public abstract class InstrumentBase : MonoBehaviour, IPlayable
{
    protected readonly struct NotePlayback
    {
        public NotePlayback(AudioClip clip, float pitch, float volume)
        {
            Clip = clip;
            Pitch = pitch;
            Volume = volume;
        }

        public AudioClip Clip { get; }
        public float Pitch { get; }
        public float Volume { get; }
    }

    [Tooltip("이 악기에서 출력될 스피커(Voice Pool) 컴포넌트입니다. 생략 시 자식에서 자동 탐색합니다.")]
    [SerializeField] protected InstrumentAudioOutput audioOutput;

    [Tooltip("이 악기에서 사용할 오디오 클립 목록입니다.")]
    [SerializeField] AudioClip[] soundClips = System.Array.Empty<AudioClip>();

    Dictionary<string, AudioClip> audioBank;

    protected virtual void Awake()
    {
        if (audioOutput == null)
            audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);

        Initialize();
    }

    protected virtual void Initialize()
    {
        if (audioOutput == null)
        {
            Debug.LogError($"[{gameObject.name}] InstrumentAudioOutput child is missing.", this);
            enabled = false;
            return;
        }

        InstrumentAudioOutput.AudioSourceSettings settings = GetAudioSourceSettings();
        CheckSpatializerPlugin(settings);
        audioOutput.InitializePoolSettings(settings);
    }

    protected virtual InstrumentAudioOutput.AudioSourceSettings GetAudioSourceSettings()
    {
        return InstrumentAudioOutput.AudioSourceSettings.CreateDefault();
    }

    protected virtual void CheckSpatializerPlugin(InstrumentAudioOutput.AudioSourceSettings settings)
    {
        if (!settings.Spatialize)
            return;

        string currentPlugin = AudioSettings.GetSpatializerPluginName();
        if (string.IsNullOrEmpty(currentPlugin))
        {
            Debug.LogWarning(
                $"[{gameObject.name}] 'Spatialize' is enabled, but no Spatializer Plugin is selected in Project Settings -> Audio. " +
                "Please install a Spatializer SDK (e.g. Meta XR Audio SDK, Microsoft Spatializer, or Resonance Audio) and select it.");
        }
    }

    /// <summary>
    /// 외부 입력이 이 악기에 MIDI 이벤트를 전달하는 공식 창구입니다.
    /// </summary>
    public virtual void TriggerMidi(MidiEvent midiEvent)
    {
        if (audioOutput == null)
            return;

        switch (midiEvent.Type)
        {
            case MidiEventType.NoteOn:
                if (TryResolveNoteOn(midiEvent, out NotePlayback playback))
                    audioOutput.PlayNote(midiEvent.Note, playback.Clip, playback.Pitch, playback.Volume);
                break;

            case MidiEventType.NoteOff:
                OnNoteOff(midiEvent);
                audioOutput.StopNote(midiEvent.Note);
                break;

            case MidiEventType.Choke:
                OnChoke(midiEvent);
                break;
        }
    }

    protected abstract bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback);

    protected virtual void OnNoteOff(MidiEvent midiEvent)
    {
    }

    /// <summary>
    /// 사운드를 즉시 정지합니다. 드럼 심벌 뮤트 등 즉각 컷이 필요한 경우 오버라이드하세요.
    /// 기본 동작은 NoteOff와 동일합니다.
    /// </summary>
    protected virtual void OnChoke(MidiEvent midiEvent)
    {
        audioOutput.StopNote(midiEvent.Note);
    }

    protected bool TryGetAudioBank(out Dictionary<string, AudioClip> bank)
    {
        if (audioBank == null)
        {
            audioBank = new Dictionary<string, AudioClip>();
            foreach (AudioClip clip in soundClips)
            {
                if (clip != null)
                    audioBank[clip.name] = clip;
            }

            if (audioBank.Count == 0)
                Debug.LogWarning($"[{GetType().Name}] soundClips가 비어 있습니다. Inspector에서 오디오 클립을 할당해 주세요.", this);
        }

        bank = audioBank;
        return bank.Count > 0;
    }

    protected static bool TryFindClipByPrefix(Dictionary<string, AudioClip> bank, string notePrefix, out AudioClip clip)
    {
        foreach (KeyValuePair<string, AudioClip> kvp in bank)
        {
            if (kvp.Key.StartsWith(notePrefix + "_") || kvp.Key.StartsWith(notePrefix + "-") || kvp.Key == notePrefix)
            {
                clip = kvp.Value;
                return true;
            }
        }

        clip = null;
        return false;
    }

    protected virtual void OnDisable()
    {
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }
}
