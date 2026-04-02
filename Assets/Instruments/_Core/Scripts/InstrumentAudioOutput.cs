using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개별 악기 3D 모델에 부착되어 실제 소리를 입체적으로 출력하는 스피커 역할을 합니다.
/// 여러 음이 겹쳐서 재생될 수 있도록 보이스 풀을 유지합니다.
/// </summary>
[DisallowMultipleComponent]
public class InstrumentAudioOutput : MonoBehaviour
{
    enum VoiceState
    {
        Idle,
        Active,
        Releasing
    }

    sealed class Voice
    {
        public AudioSource Source;
        public VoiceState State;
        public int Note = -1;
        public float StartedAt;
        public float ReleaseStartedAt;
        public float ReleaseStartVolume;
    }

    /// <summary>
    /// InstrumentBase에서 전달받아 모든 AudioSource에 일괄 적용할 설정값들입니다.
    /// </summary>
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

    readonly List<Voice> m_Voices = new List<Voice>();

    Transform m_VoicePoolRoot;
    AudioSourceSettings m_CurrentSettings = AudioSourceSettings.CreateDefault();

    void Awake()
    {
        EnsureVoicePool();
    }

    void Update()
    {
        float now = Time.time;
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.State != VoiceState.Releasing)
                continue;

            if (voice.Source == null)
            {
                ResetVoice(voice);
                continue;
            }

            if (m_CurrentSettings.ReleaseDuration <= 0f)
            {
                StopVoice(voice);
                continue;
            }

            float elapsed = now - voice.ReleaseStartedAt;
            if (elapsed >= m_CurrentSettings.ReleaseDuration)
            {
                StopVoice(voice);
                continue;
            }

            float t = 1f - (elapsed / m_CurrentSettings.ReleaseDuration);
            voice.Source.volume = voice.ReleaseStartVolume * Mathf.Clamp01(t);
        }
    }

    void OnDisable()
    {
        StopAllVoices();
    }

    public void PlayNote(int note, AudioClip clip, float pitch, float volume)
    {
        if (clip == null)
            return;

        EnsureVoicePool();

        Voice voice = GetBestVoice();
        if (voice == null || voice.Source == null)
            return;

        StopVoice(voice);

        voice.Source.clip = clip;
        voice.Source.pitch = pitch;
        voice.Source.volume = Mathf.Clamp01(volume);
        voice.Source.loop = false;
        voice.Source.Play();

        voice.Note = note;
        voice.State = VoiceState.Active;
        voice.StartedAt = Time.time;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
    }

    public void StopNote(int note)
    {
        Voice voice = GetOldestVoiceForNote(note);
        if (voice == null || voice.Source == null)
            return;

        if (m_CurrentSettings.ReleaseDuration <= 0f || !voice.Source.isPlaying)
        {
            StopVoice(voice);
            return;
        }

        voice.State = VoiceState.Releasing;
        voice.ReleaseStartedAt = Time.time;
        voice.ReleaseStartVolume = voice.Source.volume;
    }

    public void StopAllVoices()
    {
        for (int i = 0; i < m_Voices.Count; i++)
        {
            StopVoice(m_Voices[i]);
        }
    }

    public virtual void InitializePoolSettings(AudioSourceSettings settings)
    {
        m_CurrentSettings = settings;
        m_CurrentSettings.MaxVoices = Mathf.Max(1, m_CurrentSettings.MaxVoices);
        m_CurrentSettings.SpatialBlend = Mathf.Clamp01(m_CurrentSettings.SpatialBlend);
        m_CurrentSettings.ReleaseDuration = Mathf.Max(0f, m_CurrentSettings.ReleaseDuration);

        EnsureVoicePool();

        for (int i = 0; i < m_Voices.Count; i++)
        {
            ApplySettingsToSource(m_Voices[i].Source, m_CurrentSettings);
        }
    }

    void ApplySettingsToSource(AudioSource source, AudioSourceSettings settings)
    {
        if (source == null)
            return;

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
    }

    void EnsureVoicePool()
    {
        if (m_Voices.Count == m_CurrentSettings.MaxVoices && m_VoicePoolRoot != null)
            return;

        if (m_VoicePoolRoot == null)
        {
            string poolName = $"VoicePool_{gameObject.name}";
            Transform existingRoot = transform.Find(poolName);
            if (existingRoot != null)
                m_VoicePoolRoot = existingRoot;
            else
            {
                GameObject poolRoot = new GameObject(poolName);
                poolRoot.transform.SetParent(transform, false);
                m_VoicePoolRoot = poolRoot.transform;
            }
        }

        while (m_Voices.Count < m_CurrentSettings.MaxVoices)
        {
            AudioSource source = m_VoicePoolRoot.gameObject.AddComponent<AudioSource>();
            ApplySettingsToSource(source, m_CurrentSettings);

            m_Voices.Add(new Voice
            {
                Source = source,
                State = VoiceState.Idle
            });
        }
    }

    void StopVoice(Voice voice)
    {
        if (voice.Source != null)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
        }

        ResetVoice(voice);
    }

    void ResetVoice(Voice voice)
    {
        voice.State = VoiceState.Idle;
        voice.Note = -1;
        voice.StartedAt = 0f;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
    }

    Voice GetBestVoice()
    {
        Voice best = null;
        float oldestTime = float.MaxValue;

        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.State == VoiceState.Idle)
                return voice;

            if (voice.StartedAt < oldestTime)
            {
                oldestTime = voice.StartedAt;
                best = voice;
            }
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
            if (voice.Note != note || voice.Source == null || voice.State == VoiceState.Idle)
                continue;

            if (voice.StartedAt < oldestTime)
            {
                oldestTime = voice.StartedAt;
                best = voice;
            }
        }

        return best;
    }
}
