using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피아노 악기에 부착되어 각 건반 입력을 처리하고 중앙 제어기로 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class Piano : InstrumentBase
{
    private readonly HashSet<int> m_ActiveKeys = new HashSet<int>();
    private const int KeyCount = 88;
    private const int FirstMidiNote = 21;

    protected override void Initialize()
    {
        if (string.IsNullOrEmpty(resourcePath) || resourcePath == "Audio/Default") resourcePath = "Audio/Piano";
        instrumentType = InstrumentType.Melodic;
        if (string.IsNullOrEmpty(mixerGroupName)) mixerGroupName = "Piano";
        base.Initialize();
        
        Debug.Log("[Piano] Initialized with unified TriggerMidi support.");
    }

    protected override void OnDisable()
    {
        base.OnDisable(); 
        m_ActiveKeys.Clear();
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }

    // 물리 건반 센서 등에서 호출하는 기존 API 유지
    public void NoteOn(int keyIndex, float velocity) => TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, velocity, true));
    public void NoteOff(int keyIndex) => TriggerMidi(new MidiEvent(FirstMidiNote + keyIndex, 0f, false));

    protected override void OnPlayStart(MidiEvent e)
    {
        int keyIndex = e.Note - FirstMidiNote;
        m_ActiveKeys.Add(keyIndex);
        // Pooling 확보는 Base.TriggerMidi에서 이미 수행됩니다.
    }

    protected override void OnPlayEnd(MidiEvent e)
    {
        int keyIndex = e.Note - FirstMidiNote;
        m_ActiveKeys.Remove(keyIndex);

        // 더 이상 눌린 건반이 없으면 30초 타이머 시작
        if (m_ActiveKeys.Count == 0)
        {
            StartReleaseTimer();
        }
    }

    private bool IsValidKeyIndex(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < KeyCount)
            return true;

        Debug.LogWarning($"[Piano] 유효하지 않은 건반 인덱스가 전달되었습니다: {keyIndex}", this);
        return false;
    }
}
