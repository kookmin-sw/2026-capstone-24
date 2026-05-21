using System.Collections.Generic;
using Instruments;
using RhythmGame.Data;
using RhythmGame.Runtime.Clock;
using UnityEngine;

namespace RhythmGame.Runtime
{
/// <summary>
/// 트롬본 전용 노트 디스플레이 어댑터.
/// 파셜(배음) 수(5개)만큼 패널을 생성하여 플레이어 정면에 부채꼴(arc)로 배치한다.
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

    [Tooltip("파셜 0~4의 슬라이드 0(가장 짧음) 기준 base MIDI 노트. 인덱스 순서 = 파셜 순서.")]
    [SerializeField] byte[] partialBaseMidi = { 45, 52, 57, 61, 64 };

    [Tooltip("슬라이드 포지션 수. 슬라이드 0(짧음)~N-1(길음). 색상 수와 일치시킬 것.")]
    [SerializeField] int slidePositionsPerPartial = 7;

    [Tooltip("부채꼴 배치 반경 (미터). 플레이어로부터 각 패널까지 거리.")]
    [SerializeField] float panelRadius = 0.8f;

    [Tooltip("파셜 간 수직 간격 (미터). 패널이 Y축으로 이 간격만큼 쌓인다.")]
    [SerializeField] float verticalSpacingMeters = 0.12f;

    [Tooltip("부채꼴 중심 파셜 인덱스. 0-based, 중앙 패널 기준.")]
    [SerializeField] int fanCenterPartialIndex = 2;

    [Tooltip("패널 레인 높이 (미터). Inspector에서 직접 조정.")]
    [SerializeField] float laneHeightMeters = 0.08f;

    [Tooltip("노트 스크롤 방향 패널 길이 (미터). 줄이면 전체 패널이 시야에 들어온다.")]
    [SerializeField] float panelScrollLengthMeters = 1.5f;

    readonly List<NoteDisplayPanel>     spawnedPanels  = new List<NoteDisplayPanel>();
    readonly List<InstrumentLaneConfig> runtimeConfigs = new List<InstrumentLaneConfig>();
    readonly Dictionary<byte, NoteDisplayPanel> noteToPanel = new Dictionary<byte, NoteDisplayPanel>();

    Transform _trackAnchor;
    Camera    _camera;
    // 파셜 인덱스 오프셋(p - center) 저장. LateUpdate에서 verticalSpacingMeters를 곱해 Y위치 재계산.
    readonly List<float> _partialOffsets = new List<float>();

    // 테스트 호환용 (내부 전용)
    float verticalSpacing = 0.25f;
    float radius = 0.2f;

    InstrumentLaneConfig hostLaneConfig;
    int _pendingPanelCount;

    /// <summary>모든 파셜 패널의 노트가 소진되면 발생. RhythmGameHost가 자동 StopSession에 활용한다.</summary>
    public event System.Action Completed;

    /// <summary>
    /// INoteDisplayController.Begin 구현.
    /// 파셜별 패널을 플레이어 정면 부채꼴로 배치하고 슬라이드별 노트 색상을 설정한다.
    /// LaneConfig 미할당 또는 PanelPrefab 미지정이면 즉시 Completed를 발생시킨다.
    /// </summary>
    public void Begin(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        Hide();

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
        _camera = Camera.main;

        Vector3 camFwd   = GetCameraHorizontalForward();
        // Cross(up, camFwd) → 노트가 왼쪽(스폰)에서 오른쪽(판정)으로 스크롤
        Vector3 scrollUp = Vector3.Cross(Vector3.up, camFwd).normalized;
        Vector3 anchorPos = anchor.position;
        Vector3 camPos    = _camera != null ? _camera.transform.position : anchorPos;
        // 부채꼴 중심: 앵커 XZ + 카메라 높이
        Vector3 fanCenter = new Vector3(anchorPos.x, camPos.y, anchorPos.z);

        // 파셜 p마다 패널 1개 생성 (단일 레인, 슬라이드별 색상/번호)
        // 수직 부채꼴 배치: 앙각(elevation) 기준 위아래로 쌓이고, 각 패널은 Y축으로 추가 회전
        // 인접 파셜 간 겹치는 MIDI 노트는 슬라이드 거리가 가장 짧은(인덱스가 낮은) 파셜에만 표시.
        var assignedNotes = new HashSet<byte>();
        for (int p = 0; p < partialCount; p++)
        {
            float offset = (float)(p - fanCenterPartialIndex);
            _partialOffsets.Add(offset);

            // Y축 고정 간격으로 쌓기 (각도 없음)
            Vector3  panelPos    = fanCenter + camFwd * panelRadius + Vector3.up * (offset * verticalSpacingMeters);
            Vector3  dirToPlayer = (camPos - panelPos).normalized;

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
            panel.transform.rotation = Quaternion.LookRotation(dirToPlayer, scrollUp);

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

    Vector3 GetCameraHorizontalForward()
    {
        if (_camera == null) return Vector3.forward;
        Vector3 f = _camera.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
        f.Normalize();
        return f;
    }

    void LateUpdate()
    {
        if (_trackAnchor == null || spawnedPanels.Count == 0 || _camera == null) return;

        Vector3 camFwd    = GetCameraHorizontalForward();
        Vector3 scrollUp  = Vector3.Cross(Vector3.up, camFwd).normalized;
        Vector3 anchorPos = _trackAnchor.position;
        Vector3 camPos    = _camera.transform.position;
        Vector3 fanCenter = new Vector3(anchorPos.x, camPos.y, anchorPos.z);

        for (int i = 0; i < spawnedPanels.Count; i++)
        {
            NoteDisplayPanel panel = spawnedPanels[i];
            if (panel == null) continue;

            Vector3  panelPos  = fanCenter + camFwd * panelRadius + Vector3.up * (_partialOffsets[i] * verticalSpacingMeters);
            Vector3  toPlayer  = (camPos - panelPos).normalized;

            panel.transform.position = panelPos;
            panel.transform.rotation = Quaternion.LookRotation(toPlayer, scrollUp);
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
        _camera = null;
        _partialOffsets.Clear();

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

    /// <summary>테스트 호환용. 런타임 흐름에서는 사용하지 않는다.</summary>
    internal Vector3 ComputePanelWorldPos(Transform anchor, int partialIndex, int totalPartials)
    {
        Vector3 forward = anchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        float yOffset = (partialIndex - (totalPartials - 1) * 0.5f) * verticalSpacing;
        return new Vector3(
            anchor.position.x + forward.x * radius,
            anchor.position.y + yOffset,
            anchor.position.z + forward.z * radius);
    }

    /// <summary>테스트 호환용. 런타임 흐름에서는 사용하지 않는다.</summary>
    internal Quaternion ComputePanelRotation(Transform anchor, Vector3 panelWorldPos)
    {
        if (anchor == null) return Quaternion.identity;
        Vector3 inward = anchor.position - panelWorldPos;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.0001f) return Quaternion.identity;
        inward.Normalize();
        return Quaternion.LookRotation(inward, Vector3.up);
    }
}
}
