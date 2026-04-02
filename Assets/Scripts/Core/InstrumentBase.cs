using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 악기의 공통 속성과 초기화 로직을 관리하는 베이스 클래스입니다.
/// </summary>
public abstract class InstrumentBase : MonoBehaviour
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

    static readonly Dictionary<string, Dictionary<string, AudioClip>> s_AudioBanks = new Dictionary<string, Dictionary<string, AudioClip>>();

    protected abstract string DefaultResourcePath { get; }

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

        if (midiEvent.IsNoteOn)
        {
            if (TryResolveNoteOn(midiEvent, out NotePlayback playback))
            {
                audioOutput.PlayNote(midiEvent.Note, playback.Clip, playback.Pitch, playback.Volume);
            }

            return;
        }

        OnNoteOff(midiEvent);
        audioOutput.StopNote(midiEvent.Note);
    }

    protected abstract bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback);

    protected virtual void OnNoteOff(MidiEvent midiEvent)
    {
    }

    protected string GetResourcePath()
    {
        return DefaultResourcePath;
    }

    protected bool TryGetAudioBank(out Dictionary<string, AudioClip> bank)
    {
        string resourcePath = GetResourcePath();
        if (string.IsNullOrEmpty(resourcePath))
        {
            bank = null;
            return false;
        }

        if (!s_AudioBanks.TryGetValue(resourcePath, out bank))
        {
            bank = LoadAudioBank(resourcePath);
            s_AudioBanks[resourcePath] = bank;
        }

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

    Dictionary<string, AudioClip> LoadAudioBank(string path)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(path);
        Dictionary<string, AudioClip> bank = new Dictionary<string, AudioClip>();

        foreach (AudioClip clip in clips)
        {
            if (clip != null)
                bank[clip.name] = clip;
        }

        if (clips.Length == 0)
        {
            Debug.LogWarning($"[{GetType().Name}] 경로에 오디오가 존재하지 않습니다: Resources/{path}", this);
        }

        return bank;
    }

    protected virtual void OnDisable()
    {
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }
}
