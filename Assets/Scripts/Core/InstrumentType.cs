using UnityEngine;

/// <summary>
/// 중앙 제어기에서 악기를 어떻게 처리할지 결정하는 악기군 타입입니다.
/// </summary>
public enum InstrumentType
{
    /// <summary>피아노, 기타 등 Pitch를 조절하여 여러 음을 내는 악기군 (예: 음계악기)</summary>
    Melodic,
    /// <summary>드럼 등 각 MIDI 키가 자신의 고유한 타악기 소리를 가지고 있는 악기군 (예: 타악기)</summary>
    Percussion
}
