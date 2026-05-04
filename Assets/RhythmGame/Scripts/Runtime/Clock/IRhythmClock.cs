public enum RhythmClockState { Idle, Running, Paused, Stopped }

public interface IRhythmClock
{
    RhythmClockState State { get; }
    double CurrentTime { get; }

    event System.Action<RhythmClockState> StateChanged;

    void Start(VmSongChart chart);
    void Pause();
    void Resume();
    void Stop();

    double TickToSeconds(int tick);
}
