using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 악기 한 개의 레인-MIDI 노트 대응 메타데이터를 정의하는 ScriptableObject입니다.
/// 노트 디스플레이 패널(Plan 007)이 "차트의 midiNote → 레인 인덱스" 역방향 조회에 사용합니다.
/// </summary>
[CreateAssetMenu(fileName = "New_LaneConfig", menuName = "VirtualMusicStudio/Rhythm/Instrument Lane Config")]
public sealed class InstrumentLaneConfig : ScriptableObject
{
    [Serializable]
    public struct LaneEntry
    {
        [Tooltip("레인 인덱스 (0-based)")]
        public int laneIndex;

        [Tooltip("이 레인에 대응하는 MIDI 노트 번호 (0-127)")]
        public byte midiNote;
    }

    [SerializeField] List<LaneEntry> lanes = new List<LaneEntry>();

    /// <summary>
    /// lanes 리스트 중 최대 laneIndex + 1 을 반환합니다.
    /// 리스트가 비어 있으면 0을 반환합니다.
    /// </summary>
    public int LaneCount
    {
        get
        {
            int max = -1;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].laneIndex > max)
                    max = lanes[i].laneIndex;
            }
            return max + 1;
        }
    }

    /// <summary>
    /// midiNote 에 대응하는 laneIndex 를 역방향으로 조회합니다.
    /// </summary>
    /// <param name="midiNote">조회할 MIDI 노트 번호</param>
    /// <param name="laneIndex">매핑된 레인 인덱스 (미등록이면 -1)</param>
    /// <returns>매핑이 존재하면 true, 미등록이면 false</returns>
    public bool TryGetLane(byte midiNote, out int laneIndex)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            if (lanes[i].midiNote == midiNote)
            {
                laneIndex = lanes[i].laneIndex;
                return true;
            }
        }

        laneIndex = -1;
        return false;
    }
}
