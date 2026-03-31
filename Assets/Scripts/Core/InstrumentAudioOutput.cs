using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 개별 악기 3D 모델에 부착되어 실제로 소리를 입체적으로 출력하는 스피커 역할을 합니다.
/// 여러 음이 겹쳐서 재생될 수 있도록 보이스 풀(Voice Pool) 기법을 사용합니다.
/// 기존의 PianoAudioOutput이 범용화 되었습니다.
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

    [SerializeField] int maxVoices = 32;
    [SerializeField] float releaseDuration = 0.08f;
    [SerializeField] float spatialBlend = 1f;
    [SerializeField] AudioMixerGroup outputMixerGroup;

    readonly List<Voice> m_Voices = new List<Voice>();
    Transform m_VoicePoolRoot;

    void Awake()
    {
        maxVoices = Mathf.Max(1, maxVoices);
        releaseDuration = Mathf.Max(0f, releaseDuration);
        spatialBlend = Mathf.Clamp01(spatialBlend);

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

            if (releaseDuration <= 0f)
            {
                StopVoice(voice);
                continue;
            }

            float elapsed = now - voice.ReleaseStartedAt;
            if (elapsed >= releaseDuration)
            {
                StopVoice(voice);
                continue;
            }

            float t = 1f - (elapsed / releaseDuration);
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
        BeginRelease(note);

        Voice voice = GetBestVoice();
        if (voice == null || voice.Source == null)
            return;

        voice.Source.Stop();
        voice.Source.clip = clip;
        voice.Source.pitch = pitch;
        voice.Source.volume = Mathf.Clamp01(volume);
        voice.Source.spatialBlend = spatialBlend;
        voice.Source.outputAudioMixerGroup = outputMixerGroup;
        voice.Source.loop = false;
        voice.Source.Play();

        voice.Note = note;
        voice.State = VoiceState.Active;
        voice.StartedAt = Time.time;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
    }

    public void ReleaseNote(int note)
    {
        BeginRelease(note);
    }

    public void StopAllVoices()
    {
        for (int i = 0; i < m_Voices.Count; i++)
        {
            StopVoice(m_Voices[i]);
        }
    }

    /// <summary>
    /// 보이스 풀 내부의 모든 AudioSource에 동일한 초기 설정을 부여합니다.
    /// (예: 모든 오디오의 볼륨, 3D 공간 블렌드 값 등)
    /// </summary>
    public virtual void InitializePoolSettings()
    {
        EnsureVoicePool();
        for (int i = 0; i < m_Voices.Count; i++)
        {
            if (m_Voices[i].Source != null)
            {
                // 공통 설정 (현재는 spatialBlend 등 기존 값을 재확인하는 정도로 유지)
                m_Voices[i].Source.spatialBlend = spatialBlend;
                m_Voices[i].Source.outputAudioMixerGroup = outputMixerGroup;
                m_Voices[i].Source.playOnAwake = false;
            }
        }
    }

    /// <summary>
    /// 동적으로 오디오 믹서 그룹을 할당합니다.
    /// </summary>
    /// <param name="group">할당할 AudioMixerGroup</param>
    public void SetMixerGroup(AudioMixerGroup group)
    {
        outputMixerGroup = group;
        InitializePoolSettings(); // 기존 보이스 풀에도 즉시 적용
    }

    void EnsureVoicePool()
    {
        if (m_Voices.Count == maxVoices && m_VoicePoolRoot != null)
            return;

        if (m_VoicePoolRoot == null)
        {
            var existingRoot = transform.Find("VoicePool");
            if (existingRoot != null)
                m_VoicePoolRoot = existingRoot;
            else
            {
                var poolRoot = new GameObject("VoicePool");
                poolRoot.transform.SetParent(transform, false);
                m_VoicePoolRoot = poolRoot.transform;
            }
        }

        while (m_Voices.Count < maxVoices)
        {
            AudioSource source = m_VoicePoolRoot.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = outputMixerGroup;

            m_Voices.Add(new Voice
            {
                Source = source,
                State = VoiceState.Idle
            });
        }
    }

    void BeginRelease(int note)
    {
        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.State == VoiceState.Idle || voice.Note != note || voice.Source == null)
                continue;

            if (!voice.Source.isPlaying)
            {
                ResetVoice(voice);
                continue;
            }

            voice.State = VoiceState.Releasing;
            voice.ReleaseStartedAt = Time.time;
            voice.ReleaseStartVolume = voice.Source.volume;
        }
    }

    Voice GetBestVoice()
    {
        Voice bestIdle = null;
        Voice bestReleasing = null;
        Voice bestActive = null;

        for (int i = 0; i < m_Voices.Count; i++)
        {
            Voice voice = m_Voices[i];
            if (voice.Source == null)
                continue;

            if (!voice.Source.isPlaying)
            {
                ResetVoice(voice);
                if (bestIdle == null)
                    bestIdle = voice;
                continue;
            }

            if (voice.State == VoiceState.Idle)
            {
                if (bestIdle == null)
                    bestIdle = voice;
                continue;
            }

            if (voice.State == VoiceState.Releasing)
            {
                if (bestReleasing == null || voice.ReleaseStartedAt < bestReleasing.ReleaseStartedAt)
                    bestReleasing = voice;
                continue;
            }

            if (bestActive == null || voice.StartedAt < bestActive.StartedAt)
                bestActive = voice;
        }

        if (bestIdle != null)
            return bestIdle;

        if (bestReleasing != null)
        {
            StopVoice(bestReleasing);
            return bestReleasing;
        }

        if (bestActive != null)
        {
            StopVoice(bestActive);
            return bestActive;
        }

        return null;
    }

    void StopVoice(Voice voice)
    {
        if (voice.Source != null)
            voice.Source.Stop();

        ResetVoice(voice);
    }

    static void ResetVoice(Voice voice)
    {
        voice.State = VoiceState.Idle;
        voice.Note = -1;
        voice.StartedAt = 0f;
        voice.ReleaseStartedAt = 0f;
        voice.ReleaseStartVolume = 0f;
        if (voice.Source != null)
            voice.Source.volume = 0f;
    }
}
