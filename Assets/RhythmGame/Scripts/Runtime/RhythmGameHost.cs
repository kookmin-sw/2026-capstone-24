using System.Collections.Generic;
using UnityEngine;

public class RhythmGameHost : MonoBehaviour
{
    [SerializeField] RhythmSongDatabase songDatabase;
    [SerializeField] Transform uiRoot;
    [SerializeField] NoteDisplayPanel noteDisplayPanel;
    [SerializeField] InstrumentBase targetInstrument;
    [SerializeField] RhythmAccompaniment accompaniment;
    [SerializeField] float leadInSeconds = 3f;

    InstrumentBase         instrument;
    RhythmClock            clock;
    RhythmJudge            judge;
    RhythmSession          activeSession;
    INoteDisplayController activeNoteDisplay;

    IReadOnlyDictionary<int, bool> lastAccompanimentEnabled;

    public RhythmSongDatabase SongDatabase => songDatabase;
    public NoteDisplayPanel NoteDisplayPanel => noteDisplayPanel;
    public IReadOnlyDictionary<int, bool> LastAccompanimentEnabled => lastAccompanimentEnabled;

    /// <summary>StartSession() 직후 발생.</summary>
    public event System.Action SessionStarted;
    /// <summary>StopSession() 직후 발생 (자동 완료 포함).</summary>
    public event System.Action SessionEnded;

    public void SetNoteDisplayPanel(NoteDisplayPanel panel) { noteDisplayPanel = panel; }

    void Awake()
    {
        instrument = targetInstrument != null ? targetInstrument : GetComponentInParent<InstrumentBase>();
        clock = new RhythmClock(new DspTimeProvider());
        judge = new RhythmJudge(clock);
    }

    void Update()
    {
        if (activeSession != null)
            judge.Tick();
    }

    public RhythmSession StartSession(VmSongChart chart, RhythmSong song, int judgedChannel,
                                      IReadOnlyDictionary<int, bool> accompanimentEnabled = null)
    {
        lastAccompanimentEnabled = accompanimentEnabled;

        StopSession();
        float lookAhead = noteDisplayPanel != null ? noteDisplayPanel.LookAheadSeconds : 0f;
        double effectiveLeadIn = System.Math.Max(leadInSeconds, lookAhead);
        clock.Start(chart, effectiveLeadIn);
        accompaniment?.Begin(chart, judgedChannel, clock);
        judge.Start(chart, judgedChannel);
        activeSession = new RhythmSession(instrument, song, clock, judge);
        activeSession.Start();

        // 악기 본인이 자식 INoteDisplayController(예: DrumNoteDisplayAdapter)를 제공하면 우선 사용,
        // 없으면 host의 SerializedField noteDisplayPanel 폴백. 콘크리트 타입 분기 없음.
        INoteDisplayController custom = instrument != null
            ? instrument.GetComponentInChildren<INoteDisplayController>(true)
            : null;

        INoteDisplayController nextDisplay = custom != null
            ? custom
            : (INoteDisplayController)noteDisplayPanel;

        if (nextDisplay != null && (UnityEngine.Object)nextDisplay != null)
        {
            nextDisplay.Completed += OnNoteDisplayCompleted;
            nextDisplay.Begin(chart, judgedChannel, clock);
            activeNoteDisplay = nextDisplay;
            judge.Judged += activeNoteDisplay.OnJudged;
        }

        SessionStarted?.Invoke();
        return activeSession;
    }

    void OnNoteDisplayCompleted() => StopSession();

    public void StopSession()
    {
        if (activeSession != null)
        {
            if (activeNoteDisplay != null)
            {
                activeNoteDisplay.Completed -= OnNoteDisplayCompleted;
                judge.Judged -= activeNoteDisplay.OnJudged;
                activeNoteDisplay.Hide();
                activeNoteDisplay = null;
            }

            accompaniment?.End();
            activeSession.Stop();
            judge.Stop();
            clock.Stop();
            activeSession = null;

            SessionEnded?.Invoke();
        }
    }
}
