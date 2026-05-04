using UnityEngine;

public class RhythmGameHost : MonoBehaviour
{
    [SerializeField] RhythmSongDatabase songDatabase;
    [SerializeField] Transform uiRoot;

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
        return activeSession;
    }

    public void StopSession()
    {
        if (activeSession != null)
        {
            activeSession.Stop();
            judge.Stop();
            clock.Stop();
            activeSession = null;
        }
    }
}
