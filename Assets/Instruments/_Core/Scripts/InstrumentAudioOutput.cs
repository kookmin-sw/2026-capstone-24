using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Instruments
{
[DisallowMultipleComponent]
public class InstrumentAudioOutput : MonoBehaviour
{
    enum VoiceState { Idle, Attacking, Active, SustainedActive, Releasing, FadingOutDsp }

    sealed class Voice
    {
        public AudioSource Source;
        public VoiceState State;
        public int Note = -1;
        public float StartedAt;
        public float ReleaseStartedAt;
        public float ReleaseStartVolume;
        // Attack 단계 fade-in 목표 볼륨. PlayNoteSustained 진입 시 박제.
        public float TargetVolume;
        // Per-voice fade duration. NoteOn 시점에 박제돼 Attacking/Releasing 단계가 각각 사용.
        public float FadeInDuration;
        public float FadeOutDuration;
        // TrySetActiveVoicePitch가 이 voice의 pitch를 따라가게 할지. release 중에는 false로 NoteOff 시점 pitch 고정.
        public bool TrackPitch;
    }

    public struct AudioSourceSettings
    {
        public int MaxVoices;
        public bool Spatialize;
        public bool SpatializePostEffects;
        public float SpatialBlend;
        public AudioRolloffMode RolloffMode;
        public float MinDistance;
        public float MaxDistance;
        public float DopplerLevel;
        public float Spread;
        public float ReverbZoneMix;
        public float ReleaseDuration;

        public static AudioSourceSettings CreateDefault()
        {
            return new AudioSourceSettings
            {
                MaxVoices = 32,
                Spatialize = true,
                SpatializePostEffects = true,
                SpatialBlend = 1f,
                RolloffMode = AudioRolloffMode.Logarithmic,
                MinDistance = 1.5f,
                MaxDistance = 15f,
                DopplerLevel = 0f,
                Spread = 0f,
                ReverbZoneMix = 1f,
                ReleaseDuration = 0.1f
            };
        }
    }

    [Tooltip("voice AudioSource에 적용할 AudioMixerGroup. SessionMixer/Master를 할당.")]
    [SerializeField] AudioMixerGroup voiceMixerGroup;

    /// <summary>
    /// voice GameObject가 생성될 때마다 발화한다 (EnsureVoicePool 내부).
    /// 구독자는 전달된 GameObject에 외부 컴포넌트(TrombonePitchDsp 등)를 부착할 수 있다.
    /// Trombone 외 악기는 구독하지 않으므로 동작 영향 없음.
    /// </summary>
    public event System.Action<GameObject> VoiceGameObjectCreated;

    readonly List<Voice> m_Voices = new List<Voice>();
    Transform m_VoicePoolRoot;
    AudioSourceSettings m_CurrentSettings = AudioSourceSettings.CreateDefault();

    void Awake() { EnsureVoicePool(); }

    void Update()
    {
        float now = Time.time;
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.Source == null) { ResetVoice(voice); continue; }

            if (voice.State == VoiceState.Attacking)
            {
                if (voice.FadeInDuration <= 0f)
                {
                    voice.Source.volume = voice.TargetVolume;
                    voice.State = VoiceState.SustainedActive;
                    continue;
                }
                float elapsed = now - voice.StartedAt;
                if (elapsed >= voice.FadeInDuration)
                {
                    voice.Source.volume = voice.TargetVolume;
                    voice.State = VoiceState.SustainedActive;
                    continue;
                }
                float t = elapsed / voice.FadeInDuration;
                voice.Source.volume = voice.TargetVolume * Mathf.Clamp01(t);
            }
            else if (voice.State == VoiceState.Releasing)
            {
                float fadeOut = voice.FadeOutDuration > 0f ? voice.FadeOutDuration : m_CurrentSettings.ReleaseDuration;
                if (fadeOut <= 0f) { StopVoice(voice); continue; }
                float elapsed = now - voice.ReleaseStartedAt;
                if (elapsed >= fadeOut) { StopVoice(voice); continue; }
                float t = 1f - (elapsed / fadeOut);
                voice.Source.volume = voice.ReleaseStartVolume * Mathf.Clamp01(t);
            }
            else if (voice.State == VoiceState.FadingOutDsp)
            {
                if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
                {
                    if (dsp.IsStopReady)
                        StopVoice(voice);
                }
                else
                {
                    StopVoice(voice); // 안전망: DSP가 사라진 경우
                }
            }
        }
    }

    void OnDisable() { StopAllVoices(); }

    public void PlayNote(int note, AudioClip clip, float pitch, float volume)
    {
        if (clip == null) return;
        EnsureVoicePool();
        Voice voice = GetBestVoice();
        if (voice == null || voice.Source == null) return;
        StopVoice(voice);
        float targetVolume = Mathf.Clamp01(volume);
        voice.Source.clip = clip;
        voice.Source.pitch = pitch;
        voice.Source.volume = targetVolume;
        voice.Source.loop = false;
        voice.Source.Play();
        voice.Note = note;
        voice.State = VoiceState.Active;
        voice.StartedAt = Time.time;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
        voice.TargetVolume = targetVolume;
        voice.FadeInDuration = 0f;
        voice.FadeOutDuration = 0f;
        voice.TrackPitch = false;
    }

    public void PlayNoteSustained(int note, AudioClip clip, float pitch, float volume,
                                  float fadeInDuration, float fadeOutDuration)
    {
        if (clip == null) return;
        EnsureVoicePool();
        Voice voice = GetBestVoice();
        if (voice == null || voice.Source == null) return;
        StopVoice(voice);
        float targetVolume = Mathf.Clamp01(volume);
        bool hasFadeIn = fadeInDuration > 0f;
        voice.Source.clip = clip;
        voice.Source.pitch = pitch;
        voice.Source.volume = hasFadeIn ? 0f : targetVolume;
        voice.Source.loop = true;
        voice.Source.Play();
        voice.Note = note;
        voice.State = hasFadeIn ? VoiceState.Attacking : VoiceState.SustainedActive;
        voice.StartedAt = Time.time;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
        voice.TargetVolume = targetVolume;
        voice.FadeInDuration = Mathf.Max(0f, fadeInDuration);
        voice.FadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        voice.TrackPitch = true;
        // NoteOn 시 voice 재사용 직후 DSP 상태가 Idle로 reset되도록 보장.
        // TrombonePitchDsp 미부착 voice(Piano/DrumKit)는 TryGetComponent가 null 반환 → noop.
        if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dspReset))
            dspReset.ResetEnvelope();
    }

    public void StopNote(int note)
    {
        Voice voice = GetOldestVoiceForNote(note);
        if (voice == null || voice.Source == null) return;
        float fadeOut = voice.FadeOutDuration > 0f ? voice.FadeOutDuration : m_CurrentSettings.ReleaseDuration;
        if (fadeOut <= 0f || !voice.Source.isPlaying) { StopVoice(voice); return; }
        voice.State = VoiceState.Releasing;
        voice.ReleaseStartedAt = Time.time;
        voice.ReleaseStartVolume = voice.Source.volume;
        voice.TrackPitch = false;
    }

    public void StopNoteImmediate(int note)
    {
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.Note == note && voice.State != VoiceState.Idle) StopVoice(voice);
        }
    }

    /// <summary>
    /// Grip release 전용 DSP fade-out 요청. TrombonePitchDsp가 부착된 voice에 한해
    /// ≤10ms DSP fade-out 후 StopVoice를 폴링으로 회수한다.
    /// DSP 미부착 voice(Piano/DrumKit 등)는 StopNote fallback으로 처리.
    /// Choke 경로(StopNoteImmediate)와는 별개 — ARD 06 분리 정합.
    /// main thread에서만 호출.
    /// </summary>
    public void RequestGripReleaseFadeOut(int note)
    {
        Voice voice = GetOldestVoiceForNote(note);
        if (voice == null || voice.Source == null) return;
        if (voice.State == VoiceState.Idle || voice.State == VoiceState.Releasing || voice.State == VoiceState.FadingOutDsp) return;
        if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
        {
            dsp.RequestFadeOut();
            voice.State = VoiceState.FadingOutDsp;
            voice.TrackPitch = false;
        }
        else
        {
            // DSP 미부착 voice (시블링 호출 가정 없으나 안전망)
            StopNote(note);
        }
    }

    public bool TrySetActiveVoicePitch(int note, float pitch)
    {
        bool updatedAny = false;
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.Note != note || voice.Source == null) continue;
            if (voice.State != VoiceState.Active && voice.State != VoiceState.SustainedActive && voice.State != VoiceState.Attacking) continue;
            if (!voice.TrackPitch) continue;
            if (voice.Source.TryGetComponent<TrombonePitchDsp>(out var dsp))
                dsp.RequestPitchChange(pitch);
            else
                voice.Source.pitch = pitch;
            updatedAny = true;
        }
        return updatedAny;
    }

    public void StopAllVoices()
    {
        for (int i = 0; i < m_Voices.Count; i++) StopVoice(m_Voices[i]);
    }

    public virtual void InitializePoolSettings(AudioSourceSettings settings)
    {
        m_CurrentSettings = settings;
        m_CurrentSettings.MaxVoices = Mathf.Max(1, m_CurrentSettings.MaxVoices);
        m_CurrentSettings.SpatialBlend = Mathf.Clamp01(m_CurrentSettings.SpatialBlend);
        m_CurrentSettings.ReleaseDuration = Mathf.Max(0f, m_CurrentSettings.ReleaseDuration);
        EnsureVoicePool();
        for (int i = 0; i < m_Voices.Count; i++) ApplySettingsToSource(m_Voices[i].Source, m_CurrentSettings);
    }

    void ApplySettingsToSource(AudioSource source, AudioSourceSettings settings)
    {
        if (source == null) return;
        source.spatialize = settings.Spatialize;
        source.spatializePostEffects = settings.SpatializePostEffects;
        source.spatialBlend = settings.SpatialBlend;
        source.rolloffMode = settings.RolloffMode;
        source.minDistance = settings.MinDistance;
        source.maxDistance = settings.MaxDistance;
        source.dopplerLevel = settings.DopplerLevel;
        source.spread = settings.Spread;
        source.reverbZoneMix = settings.ReverbZoneMix;
        source.playOnAwake = false;
        source.loop = false;
        source.outputAudioMixerGroup = voiceMixerGroup;
    }

    void EnsureVoicePool()
    {
        if (m_Voices.Count == m_CurrentSettings.MaxVoices && m_VoicePoolRoot != null) return;
        if (m_VoicePoolRoot == null)
        {
            string poolName = string.Format("VoicePool_{0}", gameObject.name);
            Transform existingRoot = transform.Find(poolName);
            if (existingRoot != null) m_VoicePoolRoot = existingRoot;
            else
            {
                GameObject poolRoot = new GameObject(poolName);
                poolRoot.transform.SetParent(transform, false);
                m_VoicePoolRoot = poolRoot.transform;
            }
        }
        while (m_Voices.Count < m_CurrentSettings.MaxVoices)
        {
            int index = m_Voices.Count;
            GameObject voiceGo = new GameObject(string.Format("Voice_{0}", index));
            voiceGo.transform.SetParent(m_VoicePoolRoot, false);
            AudioSource source = voiceGo.AddComponent<AudioSource>();
            ApplySettingsToSource(source, m_CurrentSettings);
            Voice voice = new Voice { Source = source, State = VoiceState.Idle };
            m_Voices.Add(voice);
            VoiceGameObjectCreated?.Invoke(voiceGo);
        }
    }

    void StopVoice(Voice voice)
    {
        if (voice.Source != null) { voice.Source.Stop(); voice.Source.clip = null; }
        ResetVoice(voice);
    }

    void ResetVoice(Voice voice)
    {
        voice.State = VoiceState.Idle;
        voice.Note = -1;
        voice.StartedAt = 0f;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
        voice.TargetVolume = 0f;
        voice.FadeInDuration = 0f;
        voice.FadeOutDuration = 0f;
        voice.TrackPitch = false;
    }

    Voice GetBestVoice()
    {
        Voice best = null;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.State == VoiceState.Idle) return voice;
            if (voice.StartedAt < oldestTime) { oldestTime = voice.StartedAt; best = voice; }
        }
        return best;
    }

    Voice GetOldestVoiceForNote(int note)
    {
        Voice best = null;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.Note != note || voice.Source == null || voice.State == VoiceState.Idle) continue;
            if (voice.StartedAt < oldestTime) { oldestTime = voice.StartedAt; best = voice; }
        }
        return best;
    }
}
}
