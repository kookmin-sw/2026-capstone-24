using System.Collections.Generic;
using Instruments;
using RhythmGame.Data;
using RhythmGame.Runtime.Clock;
using UnityEngine;

namespace RhythmGame.Runtime
{
/// <summary>
/// 트롬본 전용 노트 디스플레이 어댑터.
/// 5개의 파셜(배음) 패널을 트롬본 앞 수평 반원형으로 배치하고, 차트의 MIDI 노트를
/// InstrumentLaneConfig를 통해 파셜 인덱스(0~4)로 라우팅한다.
/// INoteDisplayController를 구현하므로 RhythmGameHost가 단일 인터페이스로 제어한다.
/// </summary>
public class TromboneNoteDisplayAdapter : MonoBehaviour, INoteDisplayController
{
    [SerializeField] NoteDisplayPanel noteDisplayPanelPrefab;

    [Tooltip("패널 배치 반원 반경 (월드 단위)")]
    [SerializeField] float radius = 1.2f;

    [Tooltip("반원호 전체 각도 (도). 5패널 기준 ±arcDegrees/2 범위")]
    [SerializeField] float arcDegrees = 120f;

    [Tooltip("패널 상단을 anchor 방향으로 기울이는 각도")]
    [SerializeField, Range(0f, 70f)] float panelTiltDegrees = 30f;

    [Tooltip("패널 배치 기준 Transform. null이면 InstrumentBase.PanelAnchor로 폴백")]
    [SerializeField] Transform centerAnchor;

    [Tooltip("파셜 0~4의 슬라이드 0(가장 짧음) 기준 base MIDI 노트. 파셜 인덱스 순서와 일치해야 함.")]
    [SerializeField] byte[] partialBaseMidi = { 45, 52, 57, 61, 64 };

    [Tooltip("파셜당 슬라이드 포지션 수 (슬라이드 0~N-1, semitone 감소)")]
    [SerializeField] int slidePositionsPerPartial = 7;

    readonly List<NoteDisplayPanel>     spawnedPanels  = new List<NoteDisplayPanel>();
    readonly List<InstrumentLaneConfig> runtimeConfigs = new List<InstrumentLaneConfig>();

    // midiNote → 해당 파셜 패널 인덱스 역조회용 (OnJudged 라우팅)
    readonly Dictionary<byte, NoteDisplayPanel> noteToPanel = new Dictionary<byte, NoteDisplayPanel>();

    InstrumentLaneConfig hostLaneConfig;
    int _pendingPanelCount;

    /// <summary>모든 파셜 패널의 노트가 소진되면 발생. RhythmGameHost가 자동 StopSession에 활용한다.</summary>
    public event System.Action Completed;

    /// <summary>
    /// INoteDisplayController.Begin 구현.
    /// InstrumentBase의 LaneConfig를 읽어 5개 파셜 패널을 반원형으로 배치한다.
    /// LaneConfig 미할당 또는 PanelPrefab 미지정이면 즉시 Completed를 발생시킨다.
    /// </summary>
    public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        Hide(); // 이전 세션 잔여물 제거

        InstrumentBase host = GetComponent<InstrumentBase>();
        if (host == null) host = GetComponentInParent<InstrumentBase>();
        hostLaneConfig = host != null ? host.LaneConfig : null;

        if (hostLaneConfig == null || noteDisplayPanelPrefab == null)
        {
            Completed?.Invoke();
            return;
        }

        // centerAnchor 미지정이면 InstrumentBase.PanelAnchor로 폴백
        Transform anchor = centerAnchor;
        if (anchor == null && host != null) anchor = host.PanelAnchor;
        if (anchor == null) anchor = transform;

        int partialCount = partialBaseMidi != null ? partialBaseMidi.Length : 0;
        if (partialCount == 0)
        {
            Completed?.Invoke();
            return;
        }

        for (int i = 0; i < partialCount; i++)
        {
            byte baseMidi = partialBaseMidi[i];

            // 슬라이드 0~(slidePositionsPerPartial-1) 전체 MIDI 노트 목록 생성
            // 슬라이드 n → baseMidi - n semitones
            var notes = new List<byte>(slidePositionsPerPartial);
            for (int s = 0; s < slidePositionsPerPartial; s++)
            {
                int midi = baseMidi - s;
                if (midi >= 0 && midi <= 127)
                    notes.Add((byte)midi);
            }

            // 이 파셜의 단일-레인 런타임 LaneConfig 생성 (LaneCount == 1 → 단일-레인 모드)
            InstrumentLaneConfig singleConfig = InstrumentLaneConfig.CreateSingleLane(notes);
            runtimeConfigs.Add(singleConfig);

            // 패널 위치·회전 계산 후 인스턴스화
            Vector3 worldPos = ComputePanelWorldPos(anchor, i, partialCount);
            NoteDisplayPanel panel = Instantiate(noteDisplayPanelPrefab);
            panel.transform.position = worldPos;
            panel.transform.rotation = ComputePanelRotation(anchor, worldPos);

            panel.SetLaneConfig(singleConfig);
            panel.Show(chart, judgedChannel, clock);
            spawnedPanels.Add(panel);

            // midiNote → panel 역조회 등록 (OnJudged 라우팅용)
            foreach (byte note in notes)
            {
                if (!noteToPanel.ContainsKey(note))
                    noteToPanel[note] = panel;
            }
        }

        _pendingPanelCount = spawnedPanels.Count;
        if (_pendingPanelCount == 0)
        {
            Completed?.Invoke();
            return;
        }
        foreach (NoteDisplayPanel p in spawnedPanels)
            p.Completed += OnPanelCompleted;
    }

    void OnPanelCompleted()
    {
        _pendingPanelCount--;
        if (_pendingPanelCount <= 0)
            Completed?.Invoke();
    }

    /// <summary>
    /// 세션 종료 시 호출. 생성한 모든 패널과 런타임 SO를 정리한다.
    /// </summary>
    public void Hide()
    {
        foreach (NoteDisplayPanel panel in spawnedPanels)
        {
            if (panel == null) continue;
            panel.Completed -= OnPanelCompleted;
            panel.Hide();
            Destroy(panel.gameObject);
        }
        spawnedPanels.Clear();
        noteToPanel.Clear();
        _pendingPanelCount = 0;

        foreach (InstrumentLaneConfig cfg in runtimeConfigs)
        {
            if (cfg != null) Destroy(cfg);
        }
        runtimeConfigs.Clear();
        hostLaneConfig = null;
    }

    /// <summary>
    /// 판정 이벤트를 midiNote에 해당하는 파셜 패널로 라우팅한다.
    /// 매핑된 패널이 없는 노트는 무시한다.
    /// </summary>
    public void OnJudged(JudgmentEvent e)
    {
        if (noteToPanel.TryGetValue(e.midiNote, out NoteDisplayPanel panel) && panel != null)
            panel.OnJudged(e);
    }

    /// <summary>
    /// 파셜 인덱스 i에 해당하는 패널의 월드 포지션을 계산한다.
    /// anchor의 forward를 기준으로 arcDegrees 호 안에서 균등 배치한다.
    /// 파셜 0(가장 낮음)이 왼쪽(-arcDegrees/2), 파셜 (count-1)(가장 높음)이 오른쪽(+arcDegrees/2).
    /// </summary>
    internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex, int totalPartials)
    {
        // 5패널이면 간격 = arcDegrees / (totalPartials - 1) = 120 / 4 = 30°
        float halfArc = arcDegrees * 0.5f;
        float step = totalPartials > 1 ? arcDegrees / (totalPartials - 1) : 0f;
        float angleDeg = -halfArc + partialIndex * step;

        // anchor.forward의 수평 성분 기준으로 yaw 회전
        Vector3 forward = anchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 dir = Quaternion.Euler(0f, angleDeg, 0f) * forward;
        return anchor.position + dir * radius;
    }

    /// <summary>
    /// 패널이 anchor 중심을 향하고 panelTiltDegrees만큼 위로 기울어진 회전을 반환한다.
    /// DrumNoteDisplayAdapter.ComputePanelRotation과 동일한 방식.
    /// </summary>
    internal Quaternion ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)
    {
        if (anchor == null) return Quaternion.identity;

        Vector3 dir = anchor.position - panelWorldPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return Quaternion.identity;

        return Quaternion.LookRotation(dir.normalized, Vector3.up)
             * Quaternion.Euler(-panelTiltDegrees, 0f, 0f);
    }
}
}
