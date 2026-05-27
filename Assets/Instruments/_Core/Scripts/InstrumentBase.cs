using System;
using System.Collections.Generic;
using UnityEngine;

namespace Instruments
{
public abstract class InstrumentBase : MonoBehaviour, IPlayable, IActiveInstrument
{
    const float DefaultInstanceVolume = 0.5f;

    protected readonly struct NotePlayback
    {
        public NotePlayback(AudioClip clip, float pitch, float volume,
                            bool sustain = false,
                            float fadeInDuration = 0f, float fadeOutDuration = 0f)
        {
            Clip = clip;
            Pitch = pitch;
            Volume = volume;
            Sustain = sustain;
            FadeInDuration = fadeInDuration;
            FadeOutDuration = fadeOutDuration;
        }

        public AudioClip Clip { get; }
        public float Pitch { get; }
        public float Volume { get; }
        public bool Sustain { get; }
        public float FadeInDuration { get; }
        public float FadeOutDuration { get; }
    }

    [Tooltip("이 악기에서 출력될 스피커(Voice Pool) 컴포넌트입니다. 생략 시 자식에서 자동 탐색합니다.")]
    [SerializeField] protected InstrumentAudioOutput audioOutput;

    [Tooltip("이 악기에서 사용할 오디오 클립 목록입니다.")]
    [SerializeField] AudioClip[] soundClips = System.Array.Empty<AudioClip>();

    [Tooltip("이 악기의 레인-MIDI 노트 매핑 데이터입니다.")]
    [SerializeField] InstrumentLaneConfig laneConfig;

    [Tooltip("PlayerPrefs 키 및 IActiveInstrument.InstrumentId로 사용되는 고유 식별자입니다.")]
    [SerializeField] string instrumentId = "";

    [Tooltip("인스턴스 볼륨 (0~1).")]
    [SerializeField, Range(0f, 1f)] float instanceVolume = DefaultInstanceVolume;

    [Tooltip("세션 패널이 표시될 앵커 Transform. 미설정 시 루트 transform(바닥)을 사용합니다. 눈 높이 위치의 child 오브젝트를 할당해 주세요.")]
    [SerializeField] Transform _panelAnchor;

    [Tooltip("멀티플레이 원격 MIDI 동기화용 네트워크 악기 식별자 (ushort). 0 = 동기화 비활성.")]
    [SerializeField] ushort instrumentNetId = 0;

    [Tooltip("이 악기를 플레이하기 위해 요구되는 XR 입력 방식. Any(기본)는 제한 없음.")]
    [SerializeField] InputMode requiredInputMode = InputMode.Any;

    public InputMode RequiredInputMode => requiredInputMode;

    public InstrumentLaneConfig LaneConfig => laneConfig;

    public string InstrumentId => instrumentId;

    /// <summary>멀티플레이 브로드캐스트용 네트워크 식별자 (ushort). 0 = 동기화 비활성.</summary>
    public ushort InstrumentNetId => instrumentNetId;

    // IActiveInstrument: 패널 앵커 위치. _panelAnchor child가 설정되면 그 위치를, 아니면 루트 transform을 반환.
    public Transform PanelAnchor => _panelAnchor != null ? _panelAnchor : transform;
    public Transform InstrumentRoot => transform;

    public float InstanceVolume
    {
        get => instanceVolume;
        set
        {
            instanceVolume = Mathf.Clamp01(value);
            if (!string.IsNullOrEmpty(instrumentId))
                InstanceVolumeStore.Active.Persist(instrumentId, instanceVolume);
        }
    }

    public event Action<MidiEvent> MidiTriggered;

    Dictionary<string, AudioClip> audioBank;

    protected virtual void Awake()
    {
        if (!string.IsNullOrEmpty(instrumentId))
            instanceVolume = InstanceVolumeStore.Active.Load(instrumentId, DefaultInstanceVolume);

        if (audioOutput == null)
            audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);

        Initialize();
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
        // InstrumentId 자동 채움: caller 가 InstrumentId=0 으로 넘긴 경우 자기 instrumentNetId 로 보완.
        if (midiEvent.InstrumentId == 0 && instrumentNetId != 0)
            midiEvent = new MidiEvent(midiEvent.Note, midiEvent.Velocity, midiEvent.Type, midiEvent.Channel, instrumentNetId);

        DispatchAudio(midiEvent);
        MidiTriggered?.Invoke(midiEvent);
    }

    /// <summary>
    /// 원격 클라이언트로부터 수신한 MIDI 이벤트를 오디오로만 재생한다.
    /// MidiTriggered 이벤트는 발행하지 않으므로 RhythmGame 판정기에 흘러가지 않는다.
    /// </summary>
    public virtual void ApplyRemoteMidi(MidiEvent midiEvent)
    {
        DispatchAudio(midiEvent);
    }

    /// <summary>
    /// 시스템(자동 반주·디버그 오토플레이 등)이 생성한 MIDI 이벤트.
    /// 로컬 오디오만 재생하며 MidiTriggered 이벤트는 발행하지 않는다 — 멀티플레이어 원격 브로드캐스트 대상 아님.
    /// 각 클라이언트가 같은 곡을 동일하게 자체 재생하므로 wire 전송 시 이중 사운드 발생을 회피.
    /// </summary>
    public virtual void TriggerSystemMidi(MidiEvent midiEvent)
    {
        if (midiEvent.InstrumentId == 0 && instrumentNetId != 0)
            midiEvent = new MidiEvent(midiEvent.Note, midiEvent.Velocity, midiEvent.Type, midiEvent.Channel, instrumentNetId);

        DispatchAudio(midiEvent);
    }

    /// <summary>
    /// NoteOn / NoteOff / Choke 에 따라 오디오 출력을 처리하는 공유 헬퍼.
    /// TriggerMidi 와 ApplyRemoteMidi 양쪽에서 호출된다.
    /// NoteOn 의 finalVolume 은 자기 instanceVolume 이 자동 적용된다.
    /// </summary>
    private void DispatchAudio(MidiEvent midiEvent)
    {
        if (audioOutput == null)
            return;

        switch (midiEvent.Type)
        {
            case MidiEventType.NoteOn:
                if (TryResolveNoteOn(midiEvent, out NotePlayback playback))
                {
                    float finalVolume = playback.Volume * instanceVolume;
                    if (playback.Sustain)
                        audioOutput.PlayNoteSustained(midiEvent.Note, playback.Clip, playback.Pitch, finalVolume,
                                                      playback.FadeInDuration, playback.FadeOutDuration);
                    else
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

    protected virtual void OnEnable()
    {
        InstrumentIdRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        InstrumentIdRegistry.Unregister(this);
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }
}
}
