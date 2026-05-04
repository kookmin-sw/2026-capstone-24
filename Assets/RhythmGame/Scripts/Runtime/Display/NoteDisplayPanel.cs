using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space Canvas 위에서 88건반 피아노 레이아웃 기반 노트를 표시하는 패널.
/// 흰 건반(52개)은 각자의 레인 중앙에, 검은 건반은 인접 흰 건반 경계선에 낙하한다.
/// RhythmGameHost.StartSession() → Show(), StopSession() → Hide() 로 제어한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NoteDisplayPanel : MonoBehaviour, INoteDisplayController
{
    // ─── 88건반 상수 ─────────────────────────────────────────────────────────
    const int PIANO_MIN       = 21;   // A0
    const int PIANO_MAX       = 108;  // C8
    const int WHITE_KEY_COUNT = 52;

    static readonly HashSet<int> WhiteClasses = new HashSet<int> { 0, 2, 4, 5, 7, 9, 11 };

    // ─── Inspector ───────────────────────────────────────────────────────────
    [SerializeField] InstrumentLaneConfig laneConfig; // null = MIDI 21-108 전체 표시

    [SerializeField] float lookAheadSeconds = 2f;
    [SerializeField] float panelHeight      = 600f;   // 캔버스 SizeDelta.y

    [Header("Note Prefab (optional – plain RectTransform if null)")]
    [SerializeField] NoteVisual noteVisualPrefab;

    [Header("Note Skin Prefabs (optional – falls back to noteVisualPrefab then Image)")]
    [SerializeField] NoteVisual whiteKeySkinPrefab;
    [SerializeField] NoteVisual blackKeySkinPrefab;

    [Header("Judgment Popup (optional)")]
    [SerializeField] JudgmentPopup judgmentPopup;

    [Header("Billboard Tilt (0 = 수직 고정, >0 = 카메라 방향 + 기울기)")]
    [SerializeField, Range(0f, 70f)] float panelTiltDegrees = 0f;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    struct PendingNote
    {
        public double spawnTime;
        public double scheduledTime;
        public byte   midiNote;
        public float  durationSec;
    }

    IRhythmClock       clock;
    List<PendingNote>  pendingQueue = new List<PendingNote>();
    List<NoteVisual>   activeNotes  = new List<NoteVisual>();
    bool               layoutBuilt;
    bool               active;
    RectTransform      _panelRt;

    // ─── Lifecycle ───────────────────────────────────────────

    void Awake()
    {
        _panelRt = GetComponent<RectTransform>();
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        if (panelTiltDegrees > 0f)
        {
            var billboard = GetComponent<BillboardUI>();
            if (billboard == null) billboard = gameObject.AddComponent<BillboardUI>();
            billboard.tiltDegrees = panelTiltDegrees;
        }
    }

    // ─── 건반 분류 유틸 ──────────────────────────────────────────────────────

    static bool IsWhiteKey(byte midi) =>
        WhiteClasses.Contains(midi % 12);

    /// <summary>흰 건반 midi의 인덱스 (0 = PIANO_MIN 쪽 첫 흰 건반).</summary>
    static int GetWhiteKeyIndex(byte midi)
    {
        int n = 0;
        for (int m = PIANO_MIN; m < midi; m++)
            if (WhiteClasses.Contains(m % 12)) n++;
        return n;
    }

    /// <summary>wkIndex번째 흰 건반에 대응하는 MIDI 번호.</summary>
    static byte GetMidiForWhiteIndex(int wkIndex)
    {
        int n = 0;
        for (int m = PIANO_MIN; m <= PIANO_MAX; m++)
        {
            if (!WhiteClasses.Contains(m % 12)) continue;
            if (n == wkIndex) return (byte)m;
            n++;
        }
        return 0; // 범위 초과
    }

    /// <summary>
    /// 패널 로컬 기준 정규화 X (0 = 왼쪽, 1 = 오른쪽).
    /// 피아노가 Y=180° 회전이므로 배치 시 1−x 반전 필요.
    /// • 흰 건반 → 레인 중앙
    /// • 검은 건반 → 왼쪽 흰 건반과 오른쪽 흰 건반의 경계선
    /// </summary>
    static float NoteToNormalizedX(byte midi)
    {
        if (IsWhiteKey(midi))
        {
            return (GetWhiteKeyIndex(midi) + 0.5f) / WHITE_KEY_COUNT;
        }
        else
        {
            // 바로 왼쪽 흰 건반의 인덱스 + 1 이 경계 위치
            int leftIdx = GetWhiteKeyIndex((byte)(midi - 1));
            return (leftIdx + 1f) / WHITE_KEY_COUNT;
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public float LookAheadSeconds => lookAheadSeconds;

    /// <summary>laneConfig가 정확히 1개 노트일 때 단일 레인 모드(드럼 파츠 패널 등).</summary>
    bool IsSingleLane => laneConfig != null && laneConfig.LaneCount == 1;

    /// <summary>런타임에 laneConfig를 교체한다. Show() 호출 전에 사용해야 한다.</summary>
    public void SetLaneConfig(InstrumentLaneConfig config)
    {
        laneConfig  = config;
        layoutBuilt = false;
    }

    /// <summary>세션 시작 시 호출. 노트를 큐에 적재하고 패널을 활성화한다.</summary>
    public void Show(VmSongChart chart, int judgedChannel, IRhythmClock clock)
    {
        if (!layoutBuilt)
        {
            BuildLaneLayout();
            layoutBuilt = true;
        }

        Hide();           // 이전 세션 잔여물 정리
        layoutBuilt = true; // Hide()가 리셋하지 않도록

        this.clock = clock;
        pendingQueue.Clear();

        foreach (ChartTrack track in chart.tracks)
        {
            if (track.channel != judgedChannel) continue;

            foreach (ChartNote note in track.notes)
            {
                byte midi = (byte)note.midiNote;
                if (midi < PIANO_MIN || midi > PIANO_MAX) continue;

                // laneConfig가 있으면 등록된 노트만 표시
                if (laneConfig != null && !laneConfig.TryGetLane(midi, out _)) continue;

                double scheduledTime = clock.TickToSeconds(note.tick);
                double spawnTime     = scheduledTime - lookAheadSeconds;
                float  durationSec   = (float)(
                    clock.TickToSeconds(note.tick + note.durationTicks)
                    - clock.TickToSeconds(note.tick));

                pendingQueue.Add(new PendingNote
                {
                    spawnTime     = spawnTime,
                    scheduledTime = scheduledTime,
                    midiNote      = midi,
                    durationSec   = Mathf.Max(durationSec, 0.05f),
                });
            }
        }

        pendingQueue.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));

        active = true;
        gameObject.SetActive(true);
    }

    /// <summary>세션 종료 시 호출. 모든 노트를 즉시 제거하고 패널을 숨긴다.</summary>
    public void Hide()
    {
        active = false;

        foreach (NoteVisual nv in activeNotes)
            if (nv != null) Destroy(nv.gameObject);
        activeNotes.Clear();
        pendingQueue.Clear();

        gameObject.SetActive(false);
    }

    /// <summary>RhythmJudge.Judged 이벤트 핸들러.</summary>
    public void OnJudged(JudgmentEvent e)
    {
        if (judgmentPopup != null)
            judgmentPopup.Show(e.grade);
    }

    // ─── Update ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (!active || clock == null) return;

        double now = clock.CurrentTime;
        int i = 0;
        while (i < pendingQueue.Count)
        {
            PendingNote pn = pendingQueue[i];
            if (pn.spawnTime <= now)
            {
                SpawnNote(pn, now);
                pendingQueue.RemoveAt(i);
            }
            else { i++; }
        }

        // 하단 클리핑: 판정선 아래로 내려간 노트 즉시 파괴
        for (int j = activeNotes.Count - 1; j >= 0; j--)
        {
            NoteVisual nv = activeNotes[j];
            if (nv == null) { activeNotes.RemoveAt(j); continue; }

            var nvRt = nv.GetComponent<RectTransform>();
            float topY = (nvRt != null)
                ? nv.transform.localPosition.y + nvRt.sizeDelta.y
                : nv.transform.localPosition.y + nv.transform.localScale.y * 0.5f;

            if (topY <= _panelRt.rect.y)
            {
                Destroy(nv.gameObject);
                activeNotes.RemoveAt(j);
            }
        }
    }

    // ─── 레이아웃 빌드 ───────────────────────────────────────────────────────

    void BuildLaneLayout()
    {
        // 기존 자식 제거 (재빌드 시 중복 방지)
        for (int c = transform.childCount - 1; c >= 0; c--)
            Destroy(transform.GetChild(c).gameObject);

        // ── 단일 레인 모드(드럼 파츠 패널): 좌우 경계선 + 판정선만 구성 ──
        if (IsSingleLane)
        {
            // 판정선 (하단)
            var jlGo   = MakeUI("JudgeLine", transform);
            var jlRect = jlGo.GetComponent<RectTransform>();
            jlRect.anchorMin = new Vector2(0f, 0f);
            jlRect.anchorMax = new Vector2(1f, 0f);
            jlRect.pivot     = new Vector2(0.5f, 0f);
            jlRect.offsetMin = Vector2.zero;
            jlRect.offsetMax = new Vector2(0f, 4f);
            jlGo.AddComponent<Image>().color = new Color(1f, 1f, 0f, 0.9f);

            // 중앙 가이드선 (노트 낙하 기준)
            var centerDiv  = MakeUI("LaneDivC", transform);
            var centerRect = centerDiv.GetComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.5f, 0f);
            centerRect.anchorMax = new Vector2(0.5f, 1f);
            centerRect.pivot     = new Vector2(0.5f, 0f);
            centerRect.offsetMin = Vector2.zero;
            centerRect.offsetMax = new Vector2(1f, 0f);
            centerDiv.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);

            return;
        }

        // ── 피아노 모드: 검은 배경 + 판정선 + 88건반 구분선 ──
        var bg = MakeUI("Background", transform);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 투명

        // ── 판정선 (패널 하단) ──
        var pjlGo   = MakeUI("JudgeLine", transform);
        var pjlRect = pjlGo.GetComponent<RectTransform>();
        pjlRect.anchorMin = new Vector2(0f, 0f);
        pjlRect.anchorMax = new Vector2(1f, 0f);
        pjlRect.pivot     = new Vector2(0.5f, 0f);
        pjlRect.offsetMin = Vector2.zero;
        pjlRect.offsetMax = new Vector2(0f, 4f);
        pjlGo.AddComponent<Image>().color = new Color(1f, 1f, 0f, 0.8f);

        // ── 세로 구분선: 흰 건반 경계 (0 ~ WHITE_KEY_COUNT = 53개) ──
        // 피아노 Y=180° 반전 → normX가 클수록 화면 왼쪽
        for (int divIdx = 0; divIdx <= WHITE_KEY_COUNT; divIdx++)
        {
            float normX       = (float)divIdx / WHITE_KEY_COUNT;
            float canvasNormX = 1f - normX; // Y=180 반전

            // divIdx 번째 흰 건반이 C이면 옥타브 경계 → 밝고 굵게
            bool isCBoundary = divIdx < WHITE_KEY_COUNT
                && (GetMidiForWhiteIndex(divIdx) % 12 == 0);

            var divGo   = MakeUI($"Div_{divIdx}", transform);
            var divRect = divGo.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(canvasNormX, 0f);
            divRect.anchorMax = new Vector2(canvasNormX, 1f);
            divRect.pivot     = new Vector2(0.5f, 0f);
            divRect.offsetMin = Vector2.zero;
            divRect.offsetMax = new Vector2(isCBoundary ? 2f : 1f, 0f);
            divGo.AddComponent<Image>().color =
                new Color(1f, 1f, 1f, isCBoundary ? 0.7f : 0.2f);
        }
    }

    static GameObject MakeUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // ─── 노트 스폰 ───────────────────────────────────────────────────────────

    void SpawnNote(PendingNote pn, double now)
    {
        float fallSpeed = panelHeight / lookAheadSeconds;

        // 늦은 스폰 보정
        float elapsed = (float)(now - pn.spawnTime);
        float startY  = panelHeight - fallSpeed * elapsed;
        if (startY < 0f) return;

        // 패널 rect (피봇 무관하게 하단·좌단 절대 좌표 획득)
        Rect  pr = _panelRt.rect;
        float pw = pr.width  > 1f ? pr.width  : 1326f;

        float localX, noteW;
        if (IsSingleLane)
        {
            // 단일 레인 모드: 중앙 배치, 패널 폭의 80%
            localX = pr.x + pw * 0.5f;
            noteW  = pw * 0.80f;
        }
        else
        {
            // 피아노 모드: 건반 위치 기반 X
            float normX       = NoteToNormalizedX(pn.midiNote);
            float canvasNormX = 1f - normX;
            localX = pr.x + canvasNormX * pw;
            float wLane = pw / WHITE_KEY_COUNT;
            noteW = IsWhiteKey(pn.midiNote) ? wLane * 0.80f : wLane * 0.55f;
        }
        float localY = pr.y + startY;                          // 패널 하단 + startY
        float noteH = Mathf.Max(fallSpeed * pn.durationSec, 4f);  // min 4px guaranteed
        noteH = Mathf.Min(noteH, panelHeight);                    // panel top clipping

        NoteVisual nv;
        NoteVisual skinPrefab = IsWhiteKey(pn.midiNote) ? whiteKeySkinPrefab : blackKeySkinPrefab;
        if (skinPrefab == null) skinPrefab = noteVisualPrefab;  // fallback to generic

        if (skinPrefab != null)
        {
            nv = Instantiate(skinPrefab, transform);
        }
        else
        {
            // code-generated 2D Image (legacy fallback)
            var go = new GameObject("Note", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = IsWhiteKey(pn.midiNote)
                ? new Color(0.25f, 0.90f, 0.25f, 1f)
                : new Color(0.10f, 0.55f, 0.10f, 1f);
            nv = go.AddComponent<NoteVisual>();
        }

        // 2D (RectTransform) vs 3D branch
        var rt = nv.GetComponent<RectTransform>();
        if (rt != null)
        {
            // 2D UI mode (has RectTransform)
            rt.anchorMin  = new Vector2(0.5f, 0f);
            rt.anchorMax  = new Vector2(0.5f, 0f);
            rt.pivot      = new Vector2(0.5f, 0f);
            rt.sizeDelta  = new Vector2(noteW, noteH);
            rt.localPosition = new Vector3(localX, localY, 0f);
        }
        else
        {
            // 3D object mode
            nv.transform.localScale    = new Vector3(noteW, noteH, noteW);
            nv.transform.localPosition = new Vector3(localX, localY + noteH * 0.5f, 0f);
        }

        float noteLifetime = (startY + noteH) / fallSpeed + 0.5f;
        nv.Init(fallSpeed, noteLifetime);
        activeNotes.Add(nv);
    }

    // ─── ContextMenu (에디터 테스트용) ───────────────────────────────────────
    [ContextMenu("Debug: Force Hide")]
    void DebugHide() => Hide();
}
