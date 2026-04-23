using UnityEngine;

public readonly struct MidiEvent
{
    public MidiEvent(int note, float velocity, MidiEventType type, byte channel = 0, ushort instrumentId = 0)
    {
        Note = note;
        Velocity = Mathf.Clamp01(velocity);
        Type = type;
        Channel = channel;
        InstrumentId = instrumentId;
    }

    /// <summary>표준 MIDI 노트 번호 (0~127)</summary>
    public int Note { get; }
    /// <summary>건반/타악기를 누르거나 친 세기 (0.0 ~ 1.0)</summary>
    public float Velocity { get; }
    /// <summary>이벤트 종류 (NoteOn / NoteOff / Choke / ControlChange)</summary>
    public MidiEventType Type { get; }
    /// <summary>MIDI 채널 (0~15). 멀티플레이/MIDI export용 예약 필드.</summary>
    public byte Channel { get; }
    /// <summary>악기 인스턴스 식별자. 멀티플레이 브로드캐스트/리플레이 디스패치용 예약 필드.</summary>
    public ushort InstrumentId { get; }

    public bool IsNoteOn => Type == MidiEventType.NoteOn;
}
