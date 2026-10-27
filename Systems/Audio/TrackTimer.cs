namespace Xanadu.Systems.Audio;

/// <summary>
/// Port of Beatoraja's TIMER_PLAY math: the play origin is rewound by
/// <c>delta * (100 - speedPercent) / 100</c> each update.
/// </summary>
public sealed class TrackTimer
{
    private long _nowMicros;
    private long _playStartMicros;
    private bool _running;

    public bool IsRunning => _running;

    public long NowMicros => _running ? _nowMicros - _playStartMicros : 0;

    public long NowMs => NowMicros / 1000;

    public void Start(long nowMicros, long offsetMicros = 0)
    {
        _nowMicros = nowMicros;
        _playStartMicros = nowMicros - offsetMicros;
        _running = true;
    }

    public void Update(long nowMicros, int speedPercent)
    {
        if (!_running)
            return;

        long delta = nowMicros - _nowMicros;
        long deltaPlay = delta * (100 - speedPercent) / 100;
        _playStartMicros += deltaPlay;
        _nowMicros = nowMicros;
    }

    public void Stop()
    {
        _running = false;
    }
}
