public sealed class RhythmClock : IRhythmClock
{
    readonly ITimeProvider _provider;

    RhythmClockState _state = RhythmClockState.Idle;
    VmSongChart _chart;
    double _anchorRealTime;
    double _pausedAccumulated;

    public RhythmClock(ITimeProvider provider = null)
    {
        _provider = provider ?? new UnityTimeProvider();
    }

    public RhythmClockState State => _state;

    public double CurrentTime
    {
        get
        {
            switch (_state)
            {
                case RhythmClockState.Running:
                    return _pausedAccumulated + (_provider.Now - _anchorRealTime);
                case RhythmClockState.Paused:
                    return _pausedAccumulated;
                default:
                    return 0.0;
            }
        }
    }

    public event System.Action<RhythmClockState> StateChanged;

    public void Start(VmSongChart chart)
    {
        _chart = chart;
        _pausedAccumulated = 0.0;
        _anchorRealTime = _provider.Now;
        SetState(RhythmClockState.Running);
    }

    public void Pause()
    {
        if (_state != RhythmClockState.Running) return;

        _pausedAccumulated += _provider.Now - _anchorRealTime;
        SetState(RhythmClockState.Paused);
    }

    public void Resume()
    {
        if (_state != RhythmClockState.Paused) return;

        _anchorRealTime = _provider.Now;
        SetState(RhythmClockState.Running);
    }

    public void Stop()
    {
        if (_state == RhythmClockState.Idle) return;

        _pausedAccumulated = 0.0;
        SetState(RhythmClockState.Stopped);
    }

    public double TickToSeconds(int tick) => _chart?.tempoMap.TickToSeconds(tick) ?? 0.0;

    void SetState(RhythmClockState next)
    {
        if (_state == next) return;
        _state = next;
        StateChanged?.Invoke(_state);
    }
}
