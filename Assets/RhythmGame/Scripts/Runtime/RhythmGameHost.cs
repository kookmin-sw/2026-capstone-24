using UnityEngine;

public class RhythmGameHost : MonoBehaviour
{
    [SerializeField] RhythmSongDatabase songDatabase;
    [SerializeField] Transform uiRoot;
    [SerializeField] NoteDisplayPanel noteDisplayPanel;

    InstrumentBase instrument;
    RhythmClock    clock;
    RhythmJudge    judge;
    RhythmSession  activeSession;

    void Awake()
    {
        instrument = GetComponentInParent<InstrumentBase>();
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

        if (noteDisplayPanel != null)
            noteDisplayPanel.Show(chart, judgedChannel, clock);

        return activeSession;
    }

    public void StopSession()
    {
        if (activeSession != null)
        {
            if (noteDisplayPanel != null)
                noteDisplayPanel.Hide();

            activeSession.Stop();
            judge.Stop();
            clock.Stop();
            activeSession = null;
        }
    }
}
