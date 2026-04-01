using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using VRMusicStudio.Audio;

/// <summary>
/// 모든 악기의 공통 속성과 초기화 로직을 관리하는 베이스 클래스입니다.
/// </summary>
public abstract class InstrumentBase : MonoBehaviour
{
    [Tooltip("이 악기에서 출력될 스피커(Voice Pool) 컴포넌체입니다. 생략 시 자식에서 자동 탐색합니다.")]
    [SerializeField] protected InstrumentAudioOutput audioOutput;
    
    [Header("Instrument Settings")]
    [Tooltip("소리 샘플이 위치한 번들/리소스 폴더 상대 경로.")]
    [SerializeField] protected string resourcePath = "Audio/Default";

    [Tooltip("음계(Melodic)인지 타악기(Percussion)인지 설정")]
    [SerializeField] protected InstrumentType instrumentType = InstrumentType.Melodic;

    [Header("Mixer Pooling")]
    [Tooltip("체크 시 중앙 MixerPoolManager를 통해 동적으로 믹서 채널을 확보합니다.")]
    [SerializeField] protected bool usePooling = true;

    [Tooltip("마지막 연주 후 믹서 채널을 반납하기까지의 대기 시간(초)입니다.")]
    [SerializeField] protected float idleReleaseTime = 30.0f;

    [Tooltip("비풀링 모드일 때 사용할 고정 믹서 그룹 이름입니다.")]
    [SerializeField] protected string mixerGroupName;

    [Header("AudioSource Defaults (Shared by Voice Pool)")]
    [Tooltip("스페이셜라이저(Spatialize) 활성화 여부. HRTF 기반의 입체 음향을 위해 필수입니다.")]
    [SerializeField] protected bool spatialize = true;
    
    [Tooltip("스페이셜라이저 포스트 이펙트(Spatialize Post Effect) 활성화 여부.")]
    [SerializeField] protected bool spatializePostEffects = true;

    [Range(0f, 1f)]
    [Tooltip("2D(0)와 3D(1) 사운드의 혼합 비율입니다. VR 환경에서는 1(3D)을 권장합니다.")]
    [SerializeField] protected float spatialBlend = 1.0f;
    
    [Tooltip("사운드 감쇠 모델입니다. 거리에 따라 소리가 줄어드는 방식을 결정합니다.")]
    [SerializeField] protected AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    
    [Tooltip("합주실 크기를 고려한 최소 거리(m). 이 거리 이내에서는 소리의 높낮이가 일정하게 유지됩니다.")]
    [SerializeField] protected float minDistance = 1.5f;
    
    [Tooltip("합주실 크기를 고려한 최대 거리(m). 이 거리 이상에서는 소리가 들리지 않거나 최소치로 고정됩니다.")]
    [SerializeField] protected float maxDistance = 15.0f;
    
    [Range(0f, 5f)]
    [Tooltip("3D Sound Settings: Doppler Level (0: 비활성). 움직이는 물체의 피치 변화 정도를 조절합니다.")]
    [SerializeField] protected float dopplerLevel = 0.0f;
    
    [Range(0f, 360f)]
    [Tooltip("3D Sound Settings: Spread. 소리가 퍼지는 각도를 설정합니다 (0: 점 광원, 360: 전체 방향).")]
    [SerializeField] protected float spread = 0.0f;
    
    [Range(0f, 1.1f)]
    [Tooltip("리버브 존(Reverb Zone)의 영향을 받는 정도입니다. (0: 리버브 없음, 1: 최대 영향)")]
    [SerializeField] protected float reverbZoneMix = 1.0f;

    [Header("Release Tail Settings")]
    [Tooltip("NoteOff 시 소리가 완전히 사라질 때까지의 시간(초)입니다. (잔향 효과)")]
    [SerializeField] protected float releaseDuration = 0.1f;

    protected AudioMixerGroup currentMixerGroup;
    private Coroutine _releaseCoroutine;

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

        CheckSpatializerPlugin();
        ApplyDefaultAudioSettings();
    }

    protected virtual void CheckSpatializerPlugin()
    {
        if (spatialize)
        {
            string currentPlugin = AudioSettings.GetSpatializerPluginName();
            if (string.IsNullOrEmpty(currentPlugin))
            {
                Debug.LogWarning($"[{gameObject.name}] 'Spatialize' is enabled, but no Spatializer Plugin is selected in Project Settings -> Audio. " +
                                 "Please install a Spatializer SDK (e.g. Meta XR Audio SDK, Microsoft Spatializer, or Resonance Audio) and select it.");
            }
        }
    }

    protected virtual void ApplyDefaultAudioSettings()
    {
        if (audioOutput != null)
        {
            var settings = new InstrumentAudioOutput.AudioSourceSettings
            {
                OutputMixerGroup = currentMixerGroup,
                Spatialize = spatialize,
                SpatializePostEffects = spatializePostEffects,
                SpatialBlend = spatialBlend,
                RolloffMode = rolloffMode,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                DopplerLevel = dopplerLevel,
                Spread = spread,
                ReverbZoneMix = reverbZoneMix,
                ReleaseDuration = releaseDuration
            };

            audioOutput.InitializePoolSettings(settings);
            
            if (!usePooling)
            {
                TryAssignFixedMixerGroup();
            }

            // Debug.Log($"[{gameObject.name}] Audio settings initialized. (Spatialize: {spatialize}, Space: JamRoom)");
        }
    }

    /// <summary>
    /// 외부 컨트롤러가 이 악기에 MIDI 이벤트를 전달하는 공식 창구입니다.
    /// [중요] 테스트를 위해 소리 종류(type)와 경로(path)를 외부에서 덮어쓸 수 있도록 오버로딩을 제공합니다.
    /// </summary>
    public virtual void TriggerMidi(MidiEvent midiEvent, InstrumentType? typeOverride = null, string pathOverride = null)
    {
        // 덮어쓰기 값이 있으면 그것을 사용하고, 없으면 이 악기의 기본 설정값을 사용합니다.
        InstrumentType finalType = typeOverride ?? instrumentType;
        string finalPath = pathOverride ?? resourcePath;

        if (midiEvent.IsNoteOn)
        {
            AcquireMixerGroup();
            OnPlayStart(midiEvent);
        }
        else
        {
            OnPlayEnd(midiEvent);
        }

        // 실제 소리 재생 로직 위임 (덮어쓰기된 정보 전달)
        CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, finalType, finalPath, audioOutput);
    }

    protected abstract void OnPlayStart(MidiEvent e);
    protected abstract void OnPlayEnd(MidiEvent e);

    public virtual void AcquireMixerGroup()
    {
        if (!usePooling || currentMixerGroup != null) 
        {
            if (currentMixerGroup != null) ResetReleaseTimer();
            return;
        }

        currentMixerGroup = MixerPoolManager.Instance.RequestGroup();
        if (currentMixerGroup != null)
        {
            audioOutput.SetMixerGroup(currentMixerGroup);
            Debug.Log($"[{gameObject.name}] Acquired MixerGroup: {currentMixerGroup.name}");
        }
        
        ResetReleaseTimer();
    }

    public virtual void ReleaseMixerGroup()
    {
        if (!usePooling || currentMixerGroup == null) return;

        Debug.Log($"[{gameObject.name}] Releasing MixerGroup: {currentMixerGroup.name}");
        MixerPoolManager.Instance.ReturnGroup(currentMixerGroup);
        currentMixerGroup = null;
        if (audioOutput != null) audioOutput.SetMixerGroup(null);
        
        _releaseCoroutine = null;
    }

    protected void ResetReleaseTimer()
    {
        if (_releaseCoroutine != null)
        {
            StopCoroutine(_releaseCoroutine);
            _releaseCoroutine = null;
        }
    }

    protected void StartReleaseTimer()
    {
        if (!usePooling || currentMixerGroup == null) return;
        
        ResetReleaseTimer();
        _releaseCoroutine = StartCoroutine(ReleaseAfterDelay());
    }

    private IEnumerator ReleaseAfterDelay()
    {
        yield return new WaitForSeconds(idleReleaseTime);
        ReleaseMixerGroup();
    }

    protected virtual void TryAssignFixedMixerGroup()
    {
        if (string.IsNullOrEmpty(mixerGroupName)) return;

        var mixer = Resources.Load<AudioMixer>("MasterMixer");
        if (mixer == null) return;

        var groups = mixer.FindMatchingGroups(mixerGroupName);
        if (groups != null && groups.Length > 0)
        {
            audioOutput.SetMixerGroup(groups[0]);
        }
    }

    protected virtual void OnDisable()
    {
        ResetReleaseTimer();
        ReleaseMixerGroup();
    }
}
