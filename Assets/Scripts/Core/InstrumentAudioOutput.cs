using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 개별 악기 3D 모델에 부착되어 실제 소리를 입체적으로 출력하는 스피커 역할을 합니다.
/// 여러 음이 겹쳐서 재생될 수 있도록 보이싱 풀(Voice Pool) 기법을 사용합니다.
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
    
    // 디버깅을 위한 고유 ID (같은 씬 내 여러 악기를 구분하기 위함)
    private string m_InstanceID;

    void Awake()
    {
        m_InstanceID = $"{gameObject.name}_{System.Guid.NewGuid().ToString().Substring(0, 4)}";
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
        
        // 어떤 인스턴스가 연주 중인지 로그를 남겨 중복 체크
        // Debug.Log($"[{m_InstanceID}] Playing Note: {note} (Mixer: {(outputMixerGroup != null ? outputMixerGroup.name : "None")})");
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

    public virtual void InitializePoolSettings()
    {
        EnsureVoicePool();
        for (int i = 0; i < m_Voices.Count; i++)
        {
            if (m_Voices[i].Source != null)
            {
                m_Voices[i].Source.spatialBlend = spatialBlend;
                m_Voices[i].Source.outputAudioMixerGroup = outputMixerGroup;
                m_Voices[i].Source.playOnAwake = false;
            }
        }
    }

    public void SetMixerGroup(AudioMixerGroup group)
    {
        outputMixerGroup = group;
        InitializePoolSettings();
        Debug.Log($"[{m_InstanceID}] SetMixerGroup: {(group != null ? group.name : "Master (NULL)")}", this);
    }

    void EnsureVoicePool()
    {
        if (m_Voices.Count == maxVoices && m_VoicePoolRoot != null)
            return;

        if (m_VoicePoolRoot == null)
        {
            string poolName = $"VoicePool_{gameObject.name}";
            var existingRoot = transform.Find(poolName);
            if (existingRoot != null)
                m_VoicePoolRoot = existingRoot;
            else
            {
                var poolRoot = new GameObject(poolName);
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

    void StopVoice(Voice voice)
    {
        if (voice.Source != null)
        {
            voice.Source.Stop();
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
            Voice v = m_Voices[i];
            if (v.State == VoiceState.Idle)
                return v;

            if (v.StartedAt < oldestTime)
            {
                oldestTime = v.StartedAt;
                best = v;
            }
        }
        return best;
    }
}
