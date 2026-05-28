using System.Collections.Generic;
using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 드럼 파츠별 색상 단일 소스 (MIDI 노트 기준). 각 파츠의 도넛 material 색과 동일하게 유지한다.
    /// 리듬게임 낙하 노트 색을 도넛과 일치시키기 위해 DrumNoteDisplayAdapter가 참조한다.
    /// 노트는 불투명(alpha=1) — 도넛의 반투명과 무관하게 색상(hue)만 일치시킨다.
    /// </summary>
    public static class DrumLaneColors
    {
        static readonly Dictionary<byte, Color> ByMidiNote = new Dictionary<byte, Color>
        {
            { 36, new Color(1f,    0f,    0f   ) }, // BassDrum    빨강
            { 38, new Color(1f,    0.5f,  0f   ) }, // Snare       주황
            { 42, new Color(1f,    0.92f, 0f   ) }, // HiHat       노랑
            { 43, new Color(0.1f,  0.85f, 0.1f ) }, // FloorTom    초록
            { 45, new Color(0.1f,  0.4f,  1f   ) }, // MidTom      파랑
            { 48, new Color(0.29f, 0f,    0.51f) }, // HighTom     남색
            { 49, new Color(0.53f, 0.81f, 0.92f) }, // CrashCymbal 하늘색
            { 51, new Color(1f,    0.25f, 0.7f ) }, // RideCymbal  분홍
        };

        /// <summary>해당 MIDI 노트의 파츠 색을 반환. 매핑이 없으면 false.</summary>
        public static bool TryGetColor(byte midiNote, out Color color)
            => ByMidiNote.TryGetValue(midiNote, out color);
    }
}
