using UnityEngine;

public class RhythmGameHost : MonoBehaviour
{
    [SerializeField] RhythmSongDatabase songDatabase;
    [SerializeField] Transform uiRoot;
    [SerializeField] NoteDisplayPanel noteDisplayPanel;
    [SerializeField] InstrumentBase targetInstrument;

    InstrumentBase         instrument;
    RhythmClock            clock;
    RhythmJudge            judge;
    RhythmSession          activeSession;
    INoteDisplayController activeNoteDisplay;

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

    public RhythmSession StartSession(VmSongChart chart, RhythmSong song, int judgedChannel)
    {
        StopSession();
        clock.Start(chart);
        judge.Start(chart, judgedChannel);
        activeSession = new RhythmSession(instrument, song, clock, judge);
        activeSession.Start();

        // 드럼이면 DrumNoteDisplayAdapter, 그 외엔 단일 NoteDisplayPanel 사용
        if (instrument is DrumKit)
        {
            DrumNoteDisplayAdapter adapter = instrument.GetComponent<DrumNoteDisplayAdapter>();
            if (adapter != null && instrument.LaneConfig != null)
            {
                adapter.Init(instrument.LaneConfig, chart, judgedChannel, clock);
                activeNoteDisplay = adapter;
            }
        }
        else if (noteDisplayPanel != null)
        {
            noteDisplayPanel.Show(chart, judgedChannel, clock);
            activeNoteDisplay = noteDisplayPanel;
        }

        if (activeNoteDisplay != null)
            judge.Judged += activeNoteDisplay.OnJudged;

        return activeSession;
    }

    public void StopSession()
    {
        if (activeSession != null)
        {
            if (activeNoteDisplay != null)
            {
                judge.Judged -= activeNoteDisplay.OnJudged;
                activeNoteDisplay.Hide();
                activeNoteDisplay = null;
            }

            activeSession.Stop();
            judge.Stop();
            clock.Stop();
            activeSession = null;
        }
    }
}
