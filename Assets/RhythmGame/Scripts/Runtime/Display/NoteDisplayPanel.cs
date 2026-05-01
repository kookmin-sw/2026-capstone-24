using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space Canvas 위에서 레인과 낙하 노트를 표시하는 핵심 패널.
/// RhythmGameHost.StartSession() → Show(), StopSession() → Hide() 로 제어한다.
/// 씬에서 초기 m_IsActive=0 으로 배치한다. Show() 호출 시 활성화된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NoteDisplayPanel : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────
    [SerializeField] InstrumentLaneConfig laneConfig;
    [SerializeField] float lookAheadSeconds = 2f;
    [SerializeField] float panelHeight = 1f;

    [Header("Note Prefab (optional – plain RectTransform if null)")]
    [SerializeField] NoteVisual noteVisualPrefab;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    struct PendingNote
    {
        public double spawnTime;    // clock 시각
        public int    laneIndex;
        public float  durationSec;
    }

    IRhythmClock       clock;
    List<PendingNote>  pendingQueue  = new List<PendingNote>();
    List<NoteVisual>   activeNotes   = new List<NoteVisual>();
    RectTransform[]    laneTransforms;
    bool               layoutBuilt;
    bool               active;

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// 세션이 시작될 때 호출. 판정 채널 트랙의 노트를 큐에 적재하고 패널을 활성화한다.
    /// </summary>
    public void Show(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        // 레인 레이아웃을 최초 Show() 시점에 1회 빌드 (lazy-init)
        if (!layoutBuilt)
        {
            BuildLaneLayout();
            layoutBuilt = true;
        }

        Hide(); // 이전 세션 잔여물 정리

        this.clock = clock;
        pendingQueue.Clear();

        // 판정 채널 트랙 탐색
        foreach (ChartTrack track in chart.tracks)
        {
            if (track.channel != judgedChannel) continue;

            foreach (ChartNote note in track.notes)
            {
                // laneConfig에 없는 midiNote는 무시 (예외 없음)
                if (!laneConfig.TryGetLane(note.midiNote, out int laneIndex))
                    continue;

                double scheduledTime = clock.TickToSeconds(note.tick);
                double spawnTime     = scheduledTime - lookAheadSeconds;
                float  durationSec   = (float)(clock.TickToSeconds(note.tick + note.durationTicks)
                                               - clock.TickToSeconds(note.tick));

                pendingQueue.Add(new PendingNote
                {
                    spawnTime   = spawnTime,
                    laneIndex   = laneIndex,
                    durationSec = Mathf.Max(durationSec, 0.05f),
                });
            }
        }

        // 스폰 시각 오름차순 정렬
        pendingQueue.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));

        active = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 세션이 종료될 때 호출. 모든 노트를 즉시 제거하고 패널을 숨긴다.
    /// </summary>
    public void Hide()
    {
        active = false;

        foreach (NoteVisual nv in activeNotes)
        {
            if (nv != null) Destroy(nv.gameObject);
        }
        activeNotes.Clear();
        pendingQueue.Clear();

        gameObject.SetActive(false);
    }

    // ─── Update ──────────────────────────────────────────────────────────────
    void Update()
    {
        if (!active || clock == null) return;

        double now = clock.CurrentTime;

        // 큐에서 스폰 시각이 된 노트를 꺼내 레인에 생성
        int i = 0;
        while (i < pendingQueue.Count)
        {
            PendingNote pn = pendingQueue[i];
            if (pn.spawnTime <= now)
            {
                SpawnNote(pn);
                pendingQueue.RemoveAt(i);
                // i 증가 없음 (RemoveAt 후 같은 인덱스 재검사)
            }
            else
            {
                i++;
            }
        }
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    void BuildLaneLayout()
    {
        if (laneConfig == null) return;

        int count = laneConfig.LaneCount;
        if (count <= 0) return;

        laneTransforms = new RectTransform[count];

        float laneWidth = 1f / count; // 정규화 너비

        for (int i = 0; i < count; i++)
        {
            // 레인 컨테이너
            GameObject laneGo = new GameObject($"Lane_{i}", typeof(RectTransform));
            laneGo.transform.SetParent(transform, false);

            RectTransform rt = laneGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(laneWidth * i,       0f);
            rt.anchorMax = new Vector2(laneWidth * (i + 1), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            laneTransforms[i] = rt;

            // 판정선 (패널 하단 10~12.5% 지점)
            GameObject judgeLineGo = new GameObject("JudgeLine", typeof(RectTransform), typeof(Image));
            judgeLineGo.transform.SetParent(laneGo.transform, false);

            RectTransform jlRect = judgeLineGo.GetComponent<RectTransform>();
            jlRect.anchorMin = new Vector2(0f, 0.10f);
            jlRect.anchorMax = new Vector2(1f, 0.125f);
            jlRect.offsetMin = Vector2.zero;
            jlRect.offsetMax = Vector2.zero;

            judgeLineGo.GetComponent<Image>().color = new Color(1f, 1f, 0f, 0.8f);
        }
    }

    void SpawnNote(PendingNote pn)
    {
        if (laneTransforms == null || pn.laneIndex < 0 || pn.laneIndex >= laneTransforms.Length) return;

        RectTransform laneRect = laneTransforms[pn.laneIndex];

        NoteVisual nv;
        if (noteVisualPrefab != null)
        {
            nv = Instantiate(noteVisualPrefab, laneRect);
        }
        else
        {
            // 프리팹 없을 때 기본 흰 사각형 생성
            GameObject go = new GameObject("Note", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(laneRect, false);
            go.GetComponent<Image>().color = Color.white;
            nv = go.AddComponent<NoteVisual>();
        }

        // 노트 크기 및 초기 위치 설정 (레인 너비의 80%, 패널 상단에서 시작)
        RectTransform noteRect = nv.GetComponent<RectTransform>();
        noteRect.anchorMin = new Vector2(0.1f, 0f);
        noteRect.anchorMax = new Vector2(0.9f, 0f);
        noteRect.pivot     = new Vector2(0.5f, 0f);
        noteRect.sizeDelta = new Vector2(0f, panelHeight * 0.05f); // 높이: 패널 5%

        float fallSpeed = panelHeight / lookAheadSeconds;
        noteRect.localPosition = new Vector3(0f, panelHeight, 0f);

        // 수명 = lookAheadSeconds + 여유 (판정선 아래로 완전히 빠질 때까지)
        float noteLifetime = lookAheadSeconds + 0.5f;

        nv.Init(fallSpeed, noteLifetime);
        activeNotes.Add(nv);
    }

    // ─── ContextMenu (에디터 테스트용) ───────────────────────────────────────
    [ContextMenu("Debug: Force Hide")]
    void DebugHide() => Hide();
}
