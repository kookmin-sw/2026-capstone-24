using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드럼 전용 패널 자동 배치 컴포넌트.
/// DrumKit의 DrumHitZone 자식 목록을 순회해 각 파츠 위에 NoteDisplayPanel을 Instantiate·배치한다.
/// INoteDisplayController를 구현하므로 RhythmGameHost가 단일 인터페이스로 제어한다.
/// </summary>
public class DrumNoteDisplayAdapter : MonoBehaviour, INoteDisplayController
{
    [SerializeField] NoteDisplayPanel noteDisplayPanelPrefab;
    [Tooltip("각 드럼 파츠 위 패널의 y 오프셋 (월드 단위)")]
    [SerializeField] float yOffset = 0.15f;
    [Tooltip("패널 상단을 카메라 반대 방향으로 기울이는 각도 (0 = 수직, 클수록 더 눕혀짐)")]
    [SerializeField, Range(0f, 70f)] float panelTiltDegrees = 50f;

    readonly List<NoteDisplayPanel>              spawnedPanels  = new List<NoteDisplayPanel>();
    readonly List<InstrumentLaneConfig>          runtimeConfigs = new List<InstrumentLaneConfig>();
    readonly Dictionary<byte, NoteDisplayPanel>  noteToPanel    = new Dictionary<byte, NoteDisplayPanel>();

    /// <summary>
    /// 드럼 세션 시작 시 호출.
    /// config에 등록된 DrumHitZone의 midiNote를 매칭해 파츠마다 패널을 생성한다.
    /// config에 없는 파츠는 패널 없이 건너뛴다 (예외 발생 안 함).
    /// </summary>
    public void Init(InstrumentLaneConfig config, VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        Hide(); // 이전 세션 잔여물 제거

        if (config == null || noteDisplayPanelPrefab == null) return;

        DrumHitZone[] hitZones = GetComponentsInChildren<DrumHitZone>(includeInactive: true);
        HashSet<byte> processedNotes = new HashSet<byte>();

        foreach (DrumHitZone zone in hitZones)
        {
            byte note = (byte)zone.MidiNote;
            if (processedNotes.Contains(note)) continue;
            if (!config.TryGetLane(note, out int _)) continue; // 매핑 없으면 건너뜀

            processedNotes.Add(note);

            // 파츠 하나에 대응하는 단일-레인 config 생성 (laneIndex=0 고정)
            InstrumentLaneConfig singleConfig = InstrumentLaneConfig.CreateSingleNote(note);
            runtimeConfigs.Add(singleConfig);

            Vector3 worldPos = ComputePanelPosition(zone.transform, zone.PanelYOffset);
            NoteDisplayPanel panel = Instantiate(noteDisplayPanelPrefab);
            panel.transform.position = worldPos;
            panel.gameObject.AddComponent<BillboardUI>().tiltDegrees = panelTiltDegrees;

            panel.SetLaneConfig(singleConfig);
            panel.Show(chart, judgedChannel, clock);
            spawnedPanels.Add(panel);
            noteToPanel[note] = panel;
        }
    }

    /// <summary>
    /// 세션 종료 시 호출. 생성한 모든 패널을 Hide 후 Destroy한다.
    /// </summary>
    public void Hide()
    {
        foreach (NoteDisplayPanel panel in spawnedPanels)
        {
            if (panel == null) continue;
            panel.Hide();
            Destroy(panel.gameObject);
        }
        spawnedPanels.Clear();
        noteToPanel.Clear();

        foreach (InstrumentLaneConfig cfg in runtimeConfigs)
        {
            if (cfg != null) Destroy(cfg);
        }
        runtimeConfigs.Clear();
    }

    /// <summary>
    /// 판정 이벤트를 해당 midiNote의 패널로 라우팅한다.
    /// 매핑된 패널이 없는 노트는 무시한다.
    /// </summary>
    public void OnJudged(JudgmentEvent e)
    {
        if (noteToPanel.TryGetValue(e.midiNote, out NoteDisplayPanel panel) && panel != null)
            panel.OnJudged(e);
    }

    Vector3 ComputePanelPosition(Transform t, float extraOffset = 0f)
    {
        float totalOffset = yOffset + extraOffset;

        // Y: 히트존 Collider 상단 (실제 타격면 높이)
        Collider col = t.GetComponentInChildren<Collider>();
        float worldY = col != null
            ? col.bounds.max.y + totalOffset
            : t.position.y + 0.1f + totalOffset;

        // XZ: DrumPiece 메쉬 Renderer bounds 중심 (시각적 중심과 일치)
        DrumPiece piece = t.GetComponentInParent<DrumPiece>();
        if (piece != null)
        {
            Renderer rend = piece.GetComponentInChildren<Renderer>();
            if (rend != null)
                return new Vector3(rend.bounds.center.x, worldY, rend.bounds.center.z);
        }

        // 폴백: Collider 또는 transform XZ
        if (col != null)
            return new Vector3(col.bounds.center.x, worldY, col.bounds.center.z);

        return new Vector3(t.position.x, worldY, t.position.z);
    }
}
