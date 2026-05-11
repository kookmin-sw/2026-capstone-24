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
    DrumNoteDisplayAdapter _activeAdapter;

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

        // 드럼이면 DrumNoteDisplayAdapter 우선, 없으면 NoteDisplayPanel 폴백
        if (instrument is DrumKit)
        {
            DrumNoteDisplayAdapter adapter = instrument.GetComponent<DrumNoteDisplayAdapter>();
            if (adapter != null && instrument.LaneConfig != null)
            {
                adapter.Init(instrument.LaneConfig, chart, judgedChannel, clock);
                adapter.Completed += OnNoteDisplayCompleted;
                _activeAdapter = adapter;
                activeNoteDisplay = adapter;
            }
            else if (noteDisplayPanel != null)
            {
                noteDisplayPanel.Completed += OnNoteDisplayCompleted;
                noteDisplayPanel.Show(chart, judgedChannel, clock);
                activeNoteDisplay = noteDisplayPanel;
            }
        }
        else if (noteDisplayPanel != null)
        {
            noteDisplayPanel.Completed += OnNoteDisplayCompleted;
            noteDisplayPanel.Show(chart, judgedChannel, clock);
            activeNoteDisplay = noteDisplayPanel;
        }

        if (activeNoteDisplay != null)
            judge.Judged += activeNoteDisplay.OnJudged;

        SessionStarted?.Invoke();
        return activeSession;
    }

    void OnNoteDisplayCompleted() => StopSession();

    public void StopSession()
    {
        if (activeSession != null)
        {
            if (_activeAdapter != null)
            {
                _activeAdapter.Completed -= OnNoteDisplayCompleted;
                _activeAdapter = null;
            }
            if (noteDisplayPanel != null)
                noteDisplayPanel.Completed -= OnNoteDisplayCompleted;

            if (activeNoteDisplay != null)
            {
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
