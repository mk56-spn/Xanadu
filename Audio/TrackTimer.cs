namespace Xanadu.Audio;

/// <summary>
/// Port of Beatoraja's TIMER_PLAY math: the play origin is rewound by
/// <c>delta * (100 - speedPercent) / 100</c> each update.
/// </summary>
public sealed class TrackTimer
{
    private long _nowMicros;
    private long _playStartMicros;
    private bool _running;
    // The part of a microsecond the integer rewind left over, carried so it does not drift.
    private long _rewindRemainder;

    public long NowMicros => _running ? _nowMicros - _playStartMicros : 0;

    public long NowMs => NowMicros / 1000;

    public void Start(long nowMicros, long offsetMicros = 0)
    {
        _nowMicros = nowMicros;
        _playStartMicros = nowMicros - offsetMicros;
        _rewindRemainder = 0;
        _running = true;
    }

    public void Update(long nowMicros, int speedPercent)
    {
        if (!_running)
            return;

        long delta = nowMicros - _nowMicros;
        long scaled = delta * (100 - speedPercent) + _rewindRemainder;
        _playStartMicros += scaled / 100;
        _rewindRemainder = scaled % 100;
        _nowMicros = nowMicros;
    }

    public void Stop() => _running = false;
}
