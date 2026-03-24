using UnityEngine;

/// <summary>
/// 물리 드럼(Drum) 세트를 대표하며 중앙 제어기(CentralInstrumentController)와 통신하는 역할을 합니다.
/// 기존의 Piano.cs와 완전히 동일한 패턴으로 동작합니다.
/// 여러 위치에 있는 센서(킥 페달, 스네어 센서 등)가 이 스크립트에게 타격 이벤트를 위임합니다.
/// </summary>
[DisallowMultipleComponent]
public class Drum : MonoBehaviour
{
    [Tooltip("이 드럼 세트에서 출력될 메인 스피커(Voice Pool) 컴포넌트입니다. 할당이 안 되어있으면 자식에서 자동 탐색합니다.")]
    [SerializeField] InstrumentAudioOutput audioOutput;
    
    [Header("Instrument Settings")]
    [Tooltip("소리 샘플이 위치한 리소스 폴더 상대 경로. (예: Audio/Drum)")]
    [SerializeField] string resourcePath = "Audio/Drum";
    
    [Tooltip("음계(Melodic)인지 타악기(Percussion)인지 설정 (드럼은 Percussion이어야 합니다)")]
    [SerializeField] InstrumentType instrumentType = InstrumentType.Percussion;

    void Awake()
    {
        if (audioOutput == null)
            audioOutput = GetComponentInChildren<InstrumentAudioOutput>(true);

        if (audioOutput == null)
        {
            Debug.LogError("[Drum] 자식 혹은 본인에게서 범용 스피커(InstrumentAudioOutput)를 찾을 수 없습니다. 인스펙터를 확인해주세요.", this);
            enabled = false;
            return;
        }
    }

    void OnDisable()
    {
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }

    /// <summary>
    /// 스틱, 손, 혹은 페달 센서가 특정 패드를 쳤음을 감지했을 때 호출합니다.
    /// (타악기 특성상 피아노와 다르게 건반을 꾹 누르고 있는 상태를 유지할 필요가 적습니다)
    /// </summary>
    /// <param name="midiNote">타격한 패드에 할당된 MIDI 노트 번호 (예: 36=Kick, 38=Snare)</param>
    /// <param name="velocity">치거나 밟은 세기 (0.0 ~ 1.0)</param>
    public void Hit(int midiNote, float velocity)
    {
        // 1. 센서에서 올라온 타격 이벤트를 MIDI 포맷으로 생성합니다.
        MidiEvent midiEvent = new MidiEvent(midiNote, velocity, true);
        
        // 2. 중앙 제어기에 이벤트를 위임합니다.
        // 중앙 제어기는 'Percussion' 타입 임을 인식하고, "Audio/Drum" 안에서 
        // 36_Kick.wav 과 같이 시작하는 오디오 클립을 Pitch 조절 없이 찾아 스피커로 출력합니다.
        CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, instrumentType, resourcePath, audioOutput);
    }

    /// <summary>
    /// 크래시(Crash)나 심벌(Cymbal)을 손으로 꽉 잡아 소리를 급격하게 차단시키는 
    /// 쵸킹(Choking) 기술을 구현할 때 외부 센서가 이 함수를 호출할 수 있습니다.
    /// </summary>
    /// <param name="midiNote">소리를 멈출 패드의 MIDI 노트 번호</param>
    public void Choke(int midiNote)
    {
        MidiEvent midiEvent = new MidiEvent(midiNote, 0f, false);
        
        // NoteOff 플래그로 전송되며 중앙 제어기가 스피커(audioOutput.ReleaseNote)에 전달하여 빠르게 소리를 감쇠시킵니다.
        CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, instrumentType, resourcePath, audioOutput);
    }
}
