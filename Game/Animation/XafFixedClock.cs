using System;

namespace Xanadu.Animation;

/// <summary>
/// The fixed timestep the rig's physics (chains, cloth) run on, so they move the
/// same at any frame rate. Frame time builds up and is spent one step at a time;
/// what is left over is how far between the last two steps to draw.
/// </summary>
internal struct XafFixedClock
{
    public const float Step = 1f / 60f;

    /// <summary>Steps a new simulation takes before it is first seen, so it starts at rest and not mid-fall.</summary>
    public const int SettleSteps = 180;

    /// <summary>Longest stretch of time caught up in one update, so a stall does not stall again.</summary>
    private const float MaxCatchUp = 0.25f;

    private float _pending;

    /// <summary>How far past the last step the frame is, 0 to 1.</summary>
    public readonly float Blend => _pending / Step;

    public void Reset() => _pending = 0f;

    public void Add(float delta) => _pending = Math.Min(_pending + Math.Max(0f, delta), MaxCatchUp);

    /// <summary>Spends one step if enough time has built up.</summary>
    public bool TryStep()
    {
        if (_pending < Step)
            return false;

        _pending -= Step;
        return true;
    }
}
