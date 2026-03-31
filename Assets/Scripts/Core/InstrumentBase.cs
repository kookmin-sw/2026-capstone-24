using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 모든 악기의 공통 속성과 초기화 로직을 관리하는 베이스 클래스입니다.
/// </summary>
public abstract class InstrumentBase : MonoBehaviour
{
    [Tooltip("이 악기에서 출력될 스피커(Voice Pool) 컴포넌트입니다. 생략 시 자식에서 자동 탐색합니다.")]
    [SerializeField] protected InstrumentAudioOutput audioOutput;
    
    [Header("Instrument Settings")]
    [Tooltip("소리 샘플이 위치한 번들/리소스 폴더 상대 경로.")]
    [SerializeField] protected string resourcePath = "Audio/Default";

    [Tooltip("음계(Melodic)인지 타악기(Percussion)인지 설정")]
    [SerializeField] protected InstrumentType instrumentType = InstrumentType.Melodic;

    [Header("Mixer Settings")]
    [Tooltip("이 악기가 사용할 AudioMixerGroup의 이름입니다. (MasterMixer 내에서 검색)")]
    [SerializeField] protected string mixerGroupName;

    protected virtual void Awake()
    {
        if (audioOutput == null)
            audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);

        Initialize();
    }

    /// <summary>
    /// 악기 공통 초기화 및 서브클래스별 특수 초기화 로직을 수행합니다.
    /// </summary>
    protected virtual void Initialize()
    {
        if (audioOutput == null)
        {
            Debug.LogError($"[{gameObject.name}] InstrumentAudioOutput child is missing.", this);
            enabled = false;
            return;
        }

        // 공통 AudioSource/AudioClip 초기화 설정 (추후 유저 요청 시 이곳에 추가)
        ApplyDefaultAudioSettings();
    }

    /// <summary>
    /// AudioSource와 AudioClip에 대한 기본 설정을 수행합니다.
    /// 모든 악기에 공통적으로 적용되는 초기 코드가 위치할 곳입니다.
    /// </summary>
    protected virtual void ApplyDefaultAudioSettings()
    {
        /* 
         * [Project Standard AudioClip Import Settings]
         * 레이턴시 최소화 및 오디오 품질 표준화를 위해 아래 설정을 인스펙터에서 권장합니다:
         * 
         * 1. Force To Mono: 체크 (MONO) - 3D 공간 연산 효율화
         * 2. Normalize: 체크 해제 (사용안함)
         * 3. Load In Background: 체크 해제 (사용안함) - 재생 시점의 동기적 준비 보장
         * 4. Ambisonic: 체크 해제 (사용안함)
         * 5. Load Type: Decompress on Load - 메모리 사용량은 늘지만 재생 시 CPU 부하 및 레이턴시 최소화
         * 6. Compression Format: PCM - 압축 해제 과정 생략으로 즉각 재생
         * 7. Sample Rate Setting: Override sample rate (44.1khz) - 오디오 표준 준수
         * 8. Preload Audio Data: 체크 해제 (사용안함) - (유저 요청에 따라 첫 재생 랙 방지를 위해 사용을 권장할 수 있으나 현재는 '사용안함' 유지)
         */

        if (audioOutput != null)
        {
            // 1. 보이스 풀 내의 AudioSource들에 공통 설정을 초기화 시점에 적용합니다.
            audioOutput.InitializePoolSettings();

            // 2. 오디오 믹서 자동 연결
            TryAssignMixerGroup();

            Debug.Log($"[{gameObject.name}] Standard audio settings documented and pool initialized.");
        }
    }

    /// <summary>
    /// Resources/MasterMixer 에서 mixerGroupName과 일치하는 그룹을 찾아 오디오 출력에 할당합니다.
    /// </summary>
    protected virtual void TryAssignMixerGroup()
    {
        if (string.IsNullOrEmpty(mixerGroupName)) return;

        // Resources 워크플로우: Resources/MasterMixer.mixer 에셋이 있어야 합니다.
        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("MasterMixer");
        if (mixer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Resources/MasterMixer 를 찾을 수 없어 믹서 그룹을 자동 할당할 수 없습니다.");
            return;
        }

        var groups = mixer.FindMatchingGroups(mixerGroupName);
        if (groups != null && groups.Length > 0)
        {
            audioOutput.SetMixerGroup(groups[0]);
            Debug.Log($"[{gameObject.name}] '{mixerGroupName}' 믹서 그룹이 자동으로 연결되었습니다.");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] '{mixerGroupName}' 이름과 일치하는 믹서 그룹을 MasterMixer에서 찾을 수 없습니다.");
        }
    }
}
