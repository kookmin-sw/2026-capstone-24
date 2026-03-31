using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 물리 오브젝트의 센서 입력을 받아 중앙 제어기(CentralInstrumentController)로 이벤트를 전달합니다.
/// 피아노 악기에 부착되어 각 건반의 MIDI 번호를 자동 계산하여 제어기로 넘깁니다.
/// </summary>
[DisallowMultipleComponent]
public class Piano : InstrumentBase
{
    readonly HashSet<int> m_ActiveKeys = new HashSet<int>();
    
    // 피아노 건반 수 (0~87 = 총 88건반)
    const int KeyCount = 88;
    // 건반 0번의 기준 MIDI 번호 (A0 음 = 21번)
    const int FirstMidiNote = 21;

    protected override void Initialize()
    {
        if (string.IsNullOrEmpty(resourcePath) || resourcePath == "Audio/Default") resourcePath = "Audio/Piano";
        instrumentType = InstrumentType.Melodic;
        if (string.IsNullOrEmpty(mixerGroupName)) mixerGroupName = "Piano";
        base.Initialize(); // Base calls ApplyDefaultAudioSettings()
        
        // 피아노 전용 초기화 로직 (필요 시)
        Debug.Log("[Piano] Specific initialization: Setting up piano keys.");
    }

    void OnDisable()
    {
        m_ActiveKeys.Clear();
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }

    public void NoteOn(int keyIndex, float velocity)
    {
        if (!IsValidKeyIndex(keyIndex)) return;
        
        // HashSet에 추가하여 중복 눌림을 방지
        if (!m_ActiveKeys.Add(keyIndex)) return;

        // 1. 센서에서 올라온 로컬 건반 번호를 국제 표준 MIDI 이벤트(21~108)로 구조화합니다.
        MidiEvent midiEvent = new MidiEvent(FirstMidiNote + keyIndex, velocity, true);
        
        // 2. 중앙 제어기에 이벤트를 위임합니다.
        // 중앙 제어기는 이 악기의 종류('Melodic')를 확인하고, 알맞은 AudioClip을 불러온 뒤(resourcePath), 
        // 오디오 스피커(audioOutput)에 소리 재생을 지시합니다.
        CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, instrumentType, resourcePath, audioOutput);
        
        // [추후 확장] 이 지점에서 다른 플레이어의 중앙 제어기 혹은 서버로 
        // Instrument ID와 MidiEvent(note, velocity)를 브로드캐스팅하는 로직을 삽입하면 네트워크 합주가 가능합니다.
    }

    public void NoteOff(int keyIndex)
    {
        if (!IsValidKeyIndex(keyIndex)) return;
        
        if (!m_ActiveKeys.Remove(keyIndex)) return;

        // Note Off 이벤트 전송 
        MidiEvent midiEvent = new MidiEvent(FirstMidiNote + keyIndex, 0f, false);
        
        CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, instrumentType, resourcePath, audioOutput);
    }

    bool IsValidKeyIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < KeyCount)
            return true;

        Debug.LogWarning($"[Piano] 유효하지 않은 건반 인덱스가 전달되었습니다: {keyIndex}", this);
        return false;
    }
}
