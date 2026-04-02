using UnityEngine;

/// <summary>
/// 모든 악기에서 공통으로 사용하는 범용 MIDI 이벤트 구조체입니다.
/// </summary>
public readonly struct MidiEvent
{
    public MidiEvent(int note, float velocity, bool isNoteOn)
    {
        Note = note;
        Velocity = Mathf.Clamp01(velocity);
        IsNoteOn = isNoteOn;
    }

    /// <summary>표준 MIDI 노트 번호 (0~127)</summary>
    public int Note { get; }
    /// <summary>건반/타악기를 누르거나 친 세기 (0.0 ~ 1.0)</summary>
    public float Velocity { get; }
    /// <summary>노트 재생 시작(On)인지, 종료(Off)인지 여부</summary>
    public bool IsNoteOn { get; }
}
