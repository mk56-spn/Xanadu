using System.Runtime.InteropServices;

namespace Xanadu.Core.Tweens;

/// <summary>What a tween drives on its target.</summary>
public enum TweenChannel : byte
{
    /// <summary>Slides the target by <see cref="Tween.Value"/>.xy. Offsets stack: every live one adds.</summary>
    Offset,

    /// <summary>Turns the target by <see cref="Tween.Value"/>.x radians. Rotations stack: every live one adds.</summary>
    Rotation,

    /// <summary>Scales the target by <see cref="Tween.Value"/>.xy (1 is unchanged). Scales stack: every live one multiplies.</summary>
    Scale,

    /// <summary>
    /// Fades the target's colour towards <see cref="Tween.Value"/> (rgba). Colours
    /// don't add: they fold in order of priority, then start, each fading over
    /// what is beneath it, so the highest (then latest) one on top wins.
    /// </summary>
    Colour,

    /// <summary>
    /// Drives nothing on the target: its weight is a number someone reads
    /// (<see cref="TweenPool.Weight"/>), such as how far a list is scrolled.
    /// </summary>
    Number,

    /// <summary>
    /// Fades the target and everything under it to <see cref="Tween.Value"/>.x
    /// of its opacity (1 is unchanged, 0 gone). Opacities stack: every live one
    /// multiplies. Unlike <see cref="Colour"/> it is a tint over what is drawn, not its colour.
    /// </summary>
    Opacity,
}

/// <summary>Whether a tween's clip plays again.</summary>
public enum TweenLoop : byte
{
    /// <summary>Plays once and holds its end.</summary>
    None,

    /// <summary>Plays again every <see cref="Tween.Every"/>, from the start each time: flashes, pulses, bobs (with a bounced clip).</summary>
    Looped,

    /// <summary>Like <see cref="Looped"/>, but each run carries on from where the last ended: staircases, scrolling. Colour plays it as <see cref="Looped"/>.</summary>
    Stacked,
}

/// <summary>
/// A <see cref="Clip"/> playing on a target: a <see cref="Value"/> applied with
/// a weight mixed from <see cref="WeightFrom"/> to <see cref="WeightTo"/> by the
/// clip's progress (the shader's <c>clip.mixed(from, to)</c>), optionally looping.
/// <para>
/// A pure function of its clock: no step, no state carried frame to frame, so
/// it lands in the same place every run, scrubs freely and evaluates the same
/// on the CPU (<see cref="Weight(float)"/>) and on the GPU (xanadu/tween.slang
/// builds the very same <c>Clip</c> from these bytes). Before its start it holds
/// its start weight and after its end its end weight, as a clip's progress
/// does: a delayed slide-in waits where it slides in from.
/// </para>
/// <para>
/// A move "in" from somewhere is that offset at weight 1 → 0; a move "by" is
/// weight 0 → 1. 48 bytes, laid out as <c>Tween</c> in xanadu/tween.slang:
/// only ever add, at the end.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Tween
{
    /// <summary>Bytes in one tween, on both sides.</summary>
    internal const int Size = 48;

    /// <summary>What it applies at full weight: an offset (xy), a rotation (x), a scale (xy) or a colour (rgba).</summary>
    public Vector4 Value;

    /// <summary>When and how it plays.</summary>
    public Clip Clip;

    public float WeightFrom;
    public float WeightTo;

    /// <summary>When it loops: a run every this long from its start; never sooner than the clip's span.</summary>
    public float Every;

    /// <summary>How many runs a looping tween makes; 0 loops forever.</summary>
    public ushort Loops;

    /// <summary>
    /// The caller's name for the layer this tween is (hover, flash, …), so it
    /// can be faded or replaced later; 0 is anonymous, never replaced.
    /// </summary>
    public ushort Key;

    public TweenChannel Channel;
    public TweenLoop Loop;

    /// <summary>For colour: higher folds later, so it sits on top. Ties go to the later start.</summary>
    public short Priority;

    /// <summary>
    /// The weight it applies at <paramref name="now"/> on its clip's clock: the
    /// clip's progress (plus the runs done so far when stacked), mixed from
    /// <see cref="WeightFrom"/> to <see cref="WeightTo"/>. <c>tweenWeight</c>
    /// in xanadu/tween.slang is this, line for line.
    /// </summary>
    public readonly float Weight(float now)
    {
        float runs = 0f;
        if (Loop != TweenLoop.None && now > Clip.Start)
        {
            float every = Math.Max(Every, Clip.Span);
            float index = MathF.Floor((now - Clip.Start) / Math.Max(every, 1e-5f));
            if (Loops != 0 && index >= Loops)
            {
                index = Loops - 1;
                now = Clip.End + index * every;
            }

            now -= index * every;
            if (Loop == TweenLoop.Stacked && Channel != TweenChannel.Colour)
                runs = index;
        }

        return WeightFrom + (WeightTo - WeightFrom) * (Clip.At(now) + runs);
    }

    /// <summary>The weight it applies now.</summary>
    public readonly float Weight() => Weight(Clip.Now);

    /// <summary>
    /// The weight it holds once done: a one-shot's at its end (reversed and
    /// bounced clips included); a looping one's end weight, which it keeps returning to.
    /// </summary>
    internal readonly float Settled => Loop == TweenLoop.None ? Weight(float.MaxValue) : WeightTo;

    /// <summary>Whether it is done changing at <paramref name="now"/>: played once and past its clip's end.</summary>
    internal readonly bool SettledAt(float now) => Loop == TweenLoop.None && now >= Clip.End;

    /// <summary>A slide by <paramref name="offset"/>: weight 0 → 1 over the clip.</summary>
    public static Tween MoveBy(Vector2 offset, Clip clip) =>
        Of(TweenChannel.Offset, new Vector4(offset.X, offset.Y, 0f, 0f), clip, 0f, 1f);

    /// <summary>A slide in from <paramref name="offset"/> to where the target rests: weight 1 → 0 over the clip.</summary>
    public static Tween MoveIn(Vector2 offset, Clip clip) =>
        Of(TweenChannel.Offset, new Vector4(offset.X, offset.Y, 0f, 0f), clip, 1f, 0f);

    /// <summary>A turn by <paramref name="radians"/>: weight 0 → 1 over the clip.</summary>
    public static Tween TurnBy(float radians, Clip clip) =>
        Of(TweenChannel.Rotation, new Vector4(radians, 0f, 0f, 0f), clip, 0f, 1f);

    /// <summary>A scale to <paramref name="scale"/> times the target's size: weight 0 → 1 over the clip.</summary>
    public static Tween ScaleTo(Vector2 scale, Clip clip) =>
        Of(TweenChannel.Scale, new Vector4(scale.X, scale.Y, 1f, 1f), clip, 0f, 1f);

    /// <summary>A fade to <paramref name="colour"/> over what is beneath: weight 0 → 1 over the clip.</summary>
    public static Tween FadeTo(Color colour, Clip clip, short priority = 0) =>
        Of(TweenChannel.Colour, new Vector4(colour.R, colour.G, colour.B, colour.A), clip, 0f, 1f) with { Priority = priority };

    /// <summary>A fade to <paramref name="opacity"/> of the target's opacity: weight 0 → 1 over the clip.</summary>
    public static Tween FadeOpacity(float opacity, Clip clip) =>
        Of(TweenChannel.Opacity, new Vector4(opacity, 0f, 0f, 0f), clip, 0f, 1f);

    /// <summary>A fade in from <paramref name="opacity"/> to the target's own: weight 1 → 0 over the clip.</summary>
    public static Tween FadeOpacityIn(float opacity, Clip clip) =>
        Of(TweenChannel.Opacity, new Vector4(opacity, 0f, 0f, 0f), clip, 1f, 0f);

    /// <summary>A number going from <paramref name="from"/> to <paramref name="to"/> over the clip.</summary>
    public static Tween Number(float from, float to, Clip clip) =>
        Of(TweenChannel.Number, Vector4.Zero, clip, from, to);

    public static Tween Of(TweenChannel channel, Vector4 value, Clip clip, float weightFrom, float weightTo) => new()
    {
        Value = value,
        Clip = clip,
        WeightFrom = weightFrom,
        WeightTo = weightTo,
        Channel = channel,
    };

    /// <summary>A copy that plays again every <paramref name="every"/> (0: back to back), <paramref name="runs"/> times (0 forever).</summary>
    public readonly Tween Looped(float every = 0f, ushort runs = 0) =>
        this with { Loop = TweenLoop.Looped, Every = every, Loops = runs };

    /// <summary>A copy that loops, each run carrying on from where the last ended.</summary>
    public readonly Tween Stacked(float every = 0f, ushort runs = 0) =>
        this with { Loop = TweenLoop.Stacked, Every = every, Loops = runs };

    /// <summary>A copy named <paramref name="key"/>, so it can be faded or replaced by name.</summary>
    public readonly Tween Named(ushort key) => this with { Key = key };
}
