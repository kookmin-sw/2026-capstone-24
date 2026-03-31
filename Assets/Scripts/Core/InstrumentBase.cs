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

        ApplyDefaultAudioSettings();
    }

    protected virtual void ApplyDefaultAudioSettings()
    {
        if (audioOutput != null)
        {
            audioOutput.InitializePoolSettings();
            
            if (!usePooling)
            {
                TryAssignFixedMixerGroup();
            }

            Debug.Log($"[{gameObject.name}] Audio settings initialized. (Pooling: {usePooling}, IdleTimeout: {idleReleaseTime}s)");
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
