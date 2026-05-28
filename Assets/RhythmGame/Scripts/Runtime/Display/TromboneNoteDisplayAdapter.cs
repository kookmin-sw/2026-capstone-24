using System.Collections.Generic;
using Instruments;
using RhythmGame.Data;
using RhythmGame.Runtime.Clock;
using UnityEngine;

namespace RhythmGame.Runtime
{
/// <summary>
/// 트롬본 전용 노트 디스플레이 어댑터.
/// 파셜(배음) 수(5개)만큼 패널을 생성하여 PanelAnchor 기준 pitch 수직 적층으로 배치한다.
/// 각 패널은 단일 레인을 가지며, 슬라이드 포지션(0~6)에 따라 노트 색상이 다르다.
/// 색상 규칙: 슬라이드 0(가장 짧음/가까움) = 보라, ..., 슬라이드 6(가장 길음/멀음) = 빨강.
/// 판정선이 현재 다가오는 노트의 슬라이드 색상으로 변경되어 시각적 가이드를 제공한다.
/// 노트는 왼쪽(스폰)에서 오른쪽(판정)으로 스크롤된다.
/// </summary>
public class TromboneNoteDisplayAdapter : MonoBehaviour, INoteDisplayController
{
    [SerializeField] NoteDisplayPanel noteDisplayPanelPrefab;

    [Tooltip("패널 배치 기준 Transform. null이면 InstrumentBase.PanelAnchor로 폴백")]
    [SerializeField] Transform centerAnchor;

    [Tooltip("파셜별 pitch 회전 축·각도의 단일 진실원. null이면 Begin에서 GetComponentInParent로 폴백.")]
    [SerializeField] TrombonePartialController partialController;

    [Tooltip("파셜 0~4의 슬라이드 0(가장 짧음) 기준 base MIDI 노트. 인덱스 순서 = 파셜 순서.")]
    [SerializeField] byte[] partialBaseMidi = { 45, 52, 57, 61, 64 };

    [Tooltip("슬라이드 포지션 수. 슬라이드 0(짧음)~N-1(길음). 색상 수와 일치시킬 것.")]
    [SerializeField] int slidePositionsPerPartial = 7;

    [Tooltip("부채꼴 배치 반경 (미터). PanelAnchor로부터 각 패널까지 거리.")]
    [SerializeField] float panelRadius = 0.8f;

    [Tooltip("[deprecated 2026-05-27] PanelAnchor 기반 pitch 적층으로 교체. partialController.AnglePerPartial / CenterPartialIndex를 사용한다.")]
    [SerializeField] float verticalSpacingMeters = 0.12f;

    [Tooltip("[deprecated 2026-05-27] PanelAnchor 기반 pitch 적층으로 교체. partialController.AnglePerPartial / CenterPartialIndex를 사용한다.")]
    [SerializeField] int fanCenterPartialIndex = 2;

    [Tooltip("패널 레인 높이 (미터). Inspector에서 직접 조정.")]
    [SerializeField] float laneHeightMeters = 0.08f;

    [Tooltip("노트 스크롤 방향 패널 길이 (미터). 줄이면 전체 패널이 시야에 들어온다.")]
    [SerializeField] float panelScrollLengthMeters = 1.5f;

    [Tooltip("판정선을 PanelAnchor yaw-forward 중앙에 정렬하기 위한 패널 중심 오프셋(미터, 양수=중앙 방향). 일반적으로 panelScrollLengthMeters/2.")]
    [SerializeField] float judgmentLineOffset = 0f;

    readonly List<NoteDisplayPanel>     spawnedPanels  = new List<NoteDisplayPanel>();
    readonly List<InstrumentLaneConfig> runtimeConfigs = new List<InstrumentLaneConfig>();
    readonly Dictionary<byte, NoteDisplayPanel> noteToPanel = new Dictionary<byte, NoteDisplayPanel>();

    Transform _trackAnchor;

    InstrumentLaneConfig hostLaneConfig;
    int _pendingPanelCount;

    /// <summary>모든 파셜 패널의 노트가 소진되면 발생. RhythmGameHost가 자동 StopSession에 활용한다.</summary>
    public event System.Action Completed;

    int GetCenterPartialIndex() => partialController != null ? partialController.CenterPartialIndex : fanCenterPartialIndex;
    float GetAnglePerPartial()  => partialController != null ? partialController.AnglePerPartial    : 15f;

    /// <summary>
    /// INoteDisplayController.Begin 구현.
    /// PanelAnchor 기준 pitch 수직 적층으로 파셜별 패널을 배치한다.
    /// LaneConfig 미할당 또는 PanelPrefab 미지정이면 즉시 Completed를 발생시킨다.
    /// </summary>
    public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        Hide();

        if (partialController == null)
            partialController = GetComponentInParent<TrombonePartialController>();

        InstrumentBase host = GetComponent<InstrumentBase>();
        if (host == null) host = GetComponentInParent<InstrumentBase>();
        hostLaneConfig = host != null ? host.LaneConfig : null;

        if (hostLaneConfig == null || noteDisplayPanelPrefab == null)
        {
            Completed?.Invoke();
            return;
        }

        Transform anchor = centerAnchor;
        if (anchor == null && host != null) anchor = host.PanelAnchor;
        if (anchor == null) anchor = transform;

        int partialCount = partialBaseMidi != null ? partialBaseMidi.Length : 0;
        if (partialCount == 0)
        {
            Completed?.Invoke();
            return;
        }

        _trackAnchor = anchor;

        // 파셜 p마다 패널 1개 생성 (단일 레인, 슬라이드별 색상/번호)
        // PanelAnchor 기준 pitch 수직 적층: 파셜 i → pitchDeg = (center - i) * anglePerPartial
        // 인접 파셜 간 겹치는 MIDI 노트는 슬라이드 거리가 가장 짧은(인덱스가 낮은) 파셜에만 표시.
        var assignedNotes = new HashSet<byte>();
        for (int p = 0; p < partialCount; p++)
        {
            Vector3    panelPos = ComputePanelWorldPos(anchor, p);
            Quaternion panelRot = ComputePanelRotation(anchor, panelPos);

            // 이 파셜의 모든 슬라이드 노트 수집 + 색상/번호 매핑
            // 이미 낮은 인덱스 파셜에 할당된 노트는 건너뜀 (중복 표시 방지)
            var notes    = new List<byte>(slidePositionsPerPartial);
            var colorMap = new Dictionary<byte, Color>(slidePositionsPerPartial);
            for (int s = 0; s < slidePositionsPerPartial; s++)
            {
                int midi = partialBaseMidi[p] - s;
                if (midi < 0 || midi > 127) continue;
                byte note = (byte)midi;
                if (assignedNotes.Contains(note)) continue;
                notes.Add(note);
                colorMap[note] = TromboneSlideColors.Palette[Mathf.Clamp(s, 0, TromboneSlideColors.Palette.Length - 1)];
            }
            foreach (byte note in notes) assignedNotes.Add(note);
            if (notes.Count == 0) continue;

            // 모든 슬라이드 노트를 단일 레인(laneIndex=0)으로 매핑
            InstrumentLaneConfig cfg = InstrumentLaneConfig.CreateSingleLane(notes);
            runtimeConfigs.Add(cfg);

            NoteDisplayPanel panel = Instantiate(noteDisplayPanelPrefab);
            panel.transform.position = panelPos;
            panel.transform.rotation = panelRot;

            // 레인 높이·길이 적용
            {
                Vector3 sc = panel.transform.localScale;
                if (laneHeightMeters > 0f)       sc.x = laneHeightMeters / 20f;
                if (panelScrollLengthMeters > 0f) sc.y = panelScrollLengthMeters / 600f;
                panel.transform.localScale = sc;
            }

            panel.NoteColorOverrides = colorMap;
            panel.SetLaneConfig(cfg);
            panel.Show(chart, judgedChannel, clock);
            spawnedPanels.Add(panel);

            foreach (byte note in notes)
                if (!noteToPanel.ContainsKey(note)) noteToPanel[note] = panel;
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

    void LateUpdate()
    {
        if (_trackAnchor == null || spawnedPanels.Count == 0) return;

        for (int i = 0; i < spawnedPanels.Count; i++)
        {
            NoteDisplayPanel panel = spawnedPanels[i];
            if (panel == null) continue;

            int        partialIndex = i;
            Vector3    panelPos     = ComputePanelWorldPos(_trackAnchor, partialIndex);
            Quaternion panelRot     = ComputePanelRotation(_trackAnchor, panelPos);

            panel.transform.position = panelPos;
            panel.transform.rotation = panelRot;
        }
    }

    void OnPanelCompleted()
    {
        _pendingPanelCount--;
        if (_pendingPanelCount <= 0)
            Completed?.Invoke();
    }

    /// <summary>세션 종료 시 호출. 생성한 모든 패널과 런타임 SO를 정리한다.</summary>
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
        _trackAnchor = null;

        foreach (InstrumentLaneConfig cfg in runtimeConfigs)
            if (cfg != null) Destroy(cfg);
        runtimeConfigs.Clear();
        hostLaneConfig = null;
    }

    /// <summary>판정 이벤트를 midiNote에 해당하는 파셜 패널로 라우팅한다.</summary>
    public void OnJudged(JudgmentEvent e)
    {
        if (noteToPanel.TryGetValue(e.midiNote, out NoteDisplayPanel panel) && panel != null)
            panel.OnJudged(e);
    }

    /// <summary>
    /// ARD 03 식: PanelAnchor 기준 pitch 수직 적층 위치 계산.
    /// anchor.forward를 XZ 수평면에 투영한 yaw-only 방향을 사용하여
    /// 트롬본 상하 틸트와 무관하게 패널을 고정 수직 부채꼴로 배치한다.
    /// panelPos = anchor.position + AngleAxis(pitchDeg, yawRight) * yawForward * radius
    /// </summary>
    internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex)
    {
        float pitchDeg = (GetCenterPartialIndex() - partialIndex) * GetAnglePerPartial();
        GetYawDirections(anchor, out Vector3 yawForward, out Vector3 yawRight);
        Vector3 basePos = anchor.position + Quaternion.AngleAxis(pitchDeg, yawRight) * yawForward * panelRadius;
        return basePos - yawRight * judgmentLineOffset;
    }

    /// <summary>
    /// 패널이 PanelAnchor를 바라보며, panel local +y(노트 스폰 방향)가
    /// 수평 right(yaw-only)와 정렬되도록 회전한다.
    /// 트롬본 상하 틸트 시 패널 방향이 변하지 않는다.
    /// </summary>
    internal Quaternion ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)
    {
        if (anchor == null) return Quaternion.identity;
        Vector3 toAnchor = anchor.position - panelWorldPos;
        if (toAnchor.sqrMagnitude < 0.0001f) return Quaternion.identity;
        toAnchor.Normalize();
        GetYawDirections(anchor, out _, out Vector3 yawRight);
        Vector3 panelUp = yawRight;
        if (Mathf.Abs(Vector3.Dot(toAnchor, panelUp)) > 0.99f) panelUp = Vector3.up;
        return Quaternion.LookRotation(toAnchor, panelUp);
    }

    /// <summary>
    /// Camera.main의 forward를 XZ 수평면에 투영해 yaw-only forward/right를 반환.
    /// 카메라를 기준으로 하면 트롬본 틸트·피치 시점 변화와 완전히 분리된다.
    /// Camera.main이 없으면 anchor.forward XZ 투영으로 폴백.
    /// </summary>
    static void GetYawDirections(Transform anchor, out Vector3 yawForward, out Vector3 yawRight)
    {
        Camera cam = Camera.main;
        Vector3 srcFwd = cam != null ? cam.transform.forward : anchor.forward;
        Vector3 fwd = new Vector3(srcFwd.x, 0f, srcFwd.z);
        if (fwd.sqrMagnitude < 0.0001f)
        {
            yawForward = Vector3.forward;
            yawRight   = Vector3.right;
        }
        else
        {
            yawForward = fwd.normalized;
            yawRight   = Vector3.Cross(Vector3.up, yawForward).normalized;
        }
    }
}
}
