using UnityEngine;

/// <summary>
/// 드럼 세트를 대표하며 중앙 제어기(CentralInstrumentController)와 통신합니다.
/// </summary>
[DisallowMultipleComponent]
public class Drum : InstrumentBase
{
    protected override void Initialize()
    {
        if (string.IsNullOrEmpty(resourcePath) || resourcePath == "Audio/Default") resourcePath = "Audio/Drum";
        instrumentType = InstrumentType.Percussion;
        if (string.IsNullOrEmpty(mixerGroupName)) mixerGroupName = "Drum";
        base.Initialize();
        
        Debug.Log("[Drum] Initialized with unified TriggerMidi support.");
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (audioOutput != null)
            audioOutput.StopAllVoices();
    }

    // 물리 타격 센서 등에서 호출하는 기존 API 유지
    public void Hit(int midiNote, float velocity) => TriggerMidi(new MidiEvent(midiNote, velocity, true));
    public void Choke(int midiNote) => TriggerMidi(new MidiEvent(midiNote, 0f, false));

    protected override void OnPlayStart(MidiEvent e)
    {
        // 타격 직후부터 30초 타이머 초기화/시작
        StartReleaseTimer();
    }

    protected override void OnPlayEnd(MidiEvent e)
    {
        // 드럼은 NoteOff 시에도 타이머를 유지/갱신합니다.
        StartReleaseTimer();
    }
}
