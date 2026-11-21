using System;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The rework's fixed rules (started 2026-11-17): one fixed floor, and a
/// hovering blue body, a rect 1 wide by 2.5 tall, that follows the curve notes it hits.
/// </summary>
public static class PlayRules
{
    /// <summary>The fixed floor's depth, playfield units up from the field's bottom edge.</summary>
    public const float FloorDepth = 300f;

    /// <summary>Where the floor's top is, in playfield units.</summary>
    public static float FloorY(Ecs.Components.Playfield playfield) => playfield.Height - FloorDepth;

    /// <summary>The body's width, playfield units.</summary>
    public const float BodyWidth = 72f;

    /// <summary>The body's height: 2.5 times its width.</summary>
    public const float BodyHeight = BodyWidth * 2.5f;

    /// <summary>The body's size, playfield units: a rect 1 wide by 2.5 tall.</summary>
    public static Vector2 BodySize => new(BodyWidth, BodyHeight);

    /// <summary>
    /// Where the body starts in a level without an entrance note: the middle of
    /// the field, halfway between its top and the floor.
    /// </summary>
    public static Vector2 EntrancePosition(Ecs.Components.Playfield playfield) =>
        new(playfield.Width / 2f, FloorY(playfield) / 2f);

    /// <summary>How far the body bobs up and down while hovering, playfield units: the slightest drift.</summary>
    public const float BobHeight = 7f;

    /// <summary>One bob, up and back down, ms.</summary>
    public const double BobPeriodMs = 1800d;

    /// <summary>How far down the hover bob has the body at a song time, playfield units (negative is up); looks only, the body's place is untouched.</summary>
    public static float Bob(double songMs) =>
        BobHeight * MathF.Sin((float)(songMs / BobPeriodMs * Math.Tau));

    /// <summary>How often the body puffs thrust out of its bottom, ms.</summary>
    public const double ThrustEveryMs = 33d;

    /// <summary>
    /// How visible the body is: not there at all until the entrance releases it.
    /// A level without an entrance shows it throughout.
    /// </summary>
    public static float BodyAlpha(double songMs, double releaseMs) =>
        !double.IsNaN(releaseMs) && songMs < releaseMs ? 0f : 1f;

    /// <summary>Roughly how long the body takes to settle on a curve's exit speed, ms; the real time is the nearest beat division to it.</summary>
    public const double ExitSettleTargetMs = 150d;

    /// <summary>The beat divisions the exit settle may take (the editor's snap divisions).</summary>
    private static readonly int[] SettleDivisions = { 1, 2, 3, 4, 6, 8, 12 };

    /// <summary>
    /// How long the body takes to settle on a curve's exit speed after the curve
    /// ends at <paramref name="endMs"/>: the beat division there closest to
    /// <see cref="ExitSettleTargetMs"/>, so the settle stays in step with the song.
    /// </summary>
    public static double ExitSettleMs(TempoMap tempo, double endMs)
    {
        double beat = TempoMap.BeatMs(tempo.At(endMs).Bpm);
        double best = beat;
        foreach (int division in SettleDivisions)
        {
            double length = beat / division;
            if (Math.Abs(length - ExitSettleTargetMs) < Math.Abs(best - ExitSettleTargetMs))
                best = length;
        }

        return best;
    }

    /// <summary>The pull down on the body, playfield units a second squared.</summary>
    public const float Gravity = 1800f;

    /// <summary>The body's cruise speed, playfield units a second.</summary>
    public const float CruiseSpeed = 900f;

    /// <summary>How fast a dash goes, playfield units a second. A placeholder.</summary>
    public const float DashSpeed = 2400f;

    /// <summary>The way a direction key dashes, unit length (y points down); zero for an action key.</summary>
    public static Vector2 DashDirection(Settings.KeybindAction key) => key switch
    {
        Settings.KeybindAction.Left => new Vector2(-1f, 0f),
        Settings.KeybindAction.Right => new Vector2(1f, 0f),
        Settings.KeybindAction.Up => new Vector2(0f, -1f),
        Settings.KeybindAction.Down => new Vector2(0f, 1f),
        _ => Vector2.Zero,
    };

    /// <summary>The body's motion as it pops out of the entrance: heading right at <see cref="CruiseSpeed"/>.</summary>
    public static Vector2 SpawnVelocity => new(CruiseSpeed, 0f);
}
