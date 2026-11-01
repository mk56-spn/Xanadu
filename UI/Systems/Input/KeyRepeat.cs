using System;

namespace Xanadu.Systems.Input;

/// <summary>
/// Turns a held direction into steps: one at once, then after a pause a stream
/// that speeds up the longer it is held. Works in time, not frames, so a slow frame
/// can owe several steps. A direction held when it is told to ignore it does
/// nothing until it is let go.
/// </summary>
public sealed class KeyRepeat(
    float delay = 0.35f,
    float slowInterval = 0.12f,
    float fastInterval = 0.04f,
    float rampSeconds = 1.5f)
{
    private int _direction;
    private bool _ignoring;
    private float _held;
    private float _untilNext;

    /// <summary>Takes the direction now held, -1, 0 or 1, and returns how many steps are due.</summary>
    public int Update(int direction, float delta)
    {
        if (direction != _direction)
        {
            _direction = direction;
            _ignoring = false;
            _held = 0;
            _untilNext = delay;
            return direction == 0 ? 0 : 1;
        }

        if (direction == 0 || _ignoring)
            return 0;

        _held += delta;
        _untilNext -= delta;
        int steps = 0;
        while (_untilNext <= 0)
        {
            steps++;
            _untilNext += Interval();
        }
        return steps;
    }

    /// <summary>Forgets the direction now held until it is released, for a key held on the way in.</summary>
    public void Ignore(int direction)
    {
        _direction = direction;
        _ignoring = direction != 0;
    }

    private float Interval()
    {
        float ramp = Math.Clamp((_held - delay) / rampSeconds, 0f, 1f);
        return slowInterval + (fastInterval - slowInterval) * ramp;
    }
}
