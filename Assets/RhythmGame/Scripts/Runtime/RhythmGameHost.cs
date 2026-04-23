using UnityEngine;

public class RhythmGameHost : MonoBehaviour
{
    [SerializeField] RhythmSongDatabase songDatabase;
    [SerializeField] Transform uiRoot;

    InstrumentBase instrument;
    RhythmSession activeSession;

    void Awake()
    {
        instrument = GetComponentInParent<InstrumentBase>();
    }

    void Update()
    {
        activeSession?.Tick(Time.deltaTime);
    }

    public RhythmSession StartSession(RhythmSong song, RhythmDifficulty difficulty)
    {
        StopSession();
        activeSession = new RhythmSession(instrument, song, difficulty);
        activeSession.Start();
        return activeSession;
    }

    public void StopSession()
    {
        activeSession?.Stop();
        activeSession = null;
    }
}
