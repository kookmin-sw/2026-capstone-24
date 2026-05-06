using System;
using System.Collections.Generic;
using UnityEngine;
using Instruments;

public abstract class InstrumentBase : MonoBehaviour, IPlayable, IActiveInstrument
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

    const string VolumeKeyPrefix = "SessionPanel.Volume.";

    [Tooltip("이 악기에서 출력될 스피커(Voice Pool) 컴포넌트입니다. 생략 시 자식에서 자동 탐색합니다.")]
    [SerializeField] protected InstrumentAudioOutput audioOutput;

    [Tooltip("이 악기에서 사용할 오디오 클립 목록입니다.")]
    [SerializeField] AudioClip[] soundClips = System.Array.Empty<AudioClip>();

    [Tooltip("이 악기의 레인-MIDI 노트 매핑 데이터입니다.")]
    [SerializeField] InstrumentLaneConfig laneConfig;

    [Tooltip("PlayerPrefs 키 및 IActiveInstrument.InstrumentId로 사용되는 고유 식별자입니다.")]
    [SerializeField] string instrumentId = "";

    [Tooltip("인스턴스 볼륨 (0~1).")]
    [SerializeField, Range(0f, 1f)] float instanceVolume = 0.5f;

    public InstrumentLaneConfig LaneConfig => laneConfig;

    public string InstrumentId => instrumentId;

    // IActiveInstrument: 패널 앵커 위치. 잡기 wiring plan에서 전용 child transform으로 교체 예정.
    public Transform PanelAnchor => transform;

    public float InstanceVolume
    {
        get => instanceVolume;
        set
        {
            instanceVolume = Mathf.Clamp01(value);
            PersistInstanceVolume();
        }
    }

    public event Action<MidiEvent> MidiTriggered;

    Dictionary<string, AudioClip> audioBank;

    protected virtual void Awake()
    {
        if (!string.IsNullOrEmpty(instrumentId))
        {
            string key = VolumeKeyPrefix + instrumentId;
            instanceVolume = PlayerPrefs.GetFloat(key, 0.5f);
        }

        if (audioOutput == null)
            audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);

        Initialize();
    }

    void PersistInstanceVolume()
    {
        if (string.IsNullOrEmpty(instrumentId))
            return;
        string key = VolumeKeyPrefix + instrumentId;
        PlayerPrefs.SetFloat(key, instanceVolume);
    }

    protected virtual void Initialize()
    {
        if (audioOutput == null)
        {
            Debug.LogError(string.Format("[{0}] InstrumentAudioOutput child is missing.", gameObject.name), this);
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
                string.Format("[{0}] 'Spatialize' is enabled, but no Spatializer Plugin is selected in Project Settings -> Audio.", gameObject.name));
        }
    }

    public virtual void TriggerMidi(MidiEvent midiEvent)
    {
        if (audioOutput == null)
            return;

        switch (midiEvent.Type)
        {
            case MidiEventType.NoteOn:
                if (TryResolveNoteOn(midiEvent, out NotePlayback playback))
                {
                    float finalVolume = playback.Volume * instanceVolume;
                    audioOutput.PlayNote(midiEvent.Note, playback.Clip, playback.Pitch, finalVolume);
                }
                break;

            case MidiEventType.NoteOff:
                OnNoteOff(midiEvent);
                audioOutput.StopNote(midiEvent.Note);
                break;

            case MidiEventType.Choke:
                OnChoke(midiEvent);
                break;
        }

        MidiTriggered?.Invoke(midiEvent);
    }

    protected abstract bool TryResolveNoteOn(MidiEvent midiEvent, out NotePlayback playback);

    protected virtual void OnNoteOff(MidiEvent midiEvent) { }

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
                Debug.LogWarning(string.Format("[{0}] soundClips가 비어 있습니다. Inspector에서 오디오 클립을 할당해 주세요.", GetType().Name), this);
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
