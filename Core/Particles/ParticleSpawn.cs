using System;

namespace Xanadu.Particles;

/// <summary>
/// One particle as it is born, in the space of the canvas item it draws in:
/// where it starts, how it is thrown, how long it waits and lives, and how it
/// looks. Whatever runs the particles moves it on from here with the effect's
/// gravity, and sizes and colours it over its life by the effect's curve and ramp.
/// </summary>
public struct ParticleSeed
{
    public Vector2 Position;
    public Vector2 Velocity;

    /// <summary>Seconds before it appears; it starts out this far below age zero.</summary>
    public float Delay;

    /// <summary>Seconds it lives once it has appeared.</summary>
    public float Lifespan;

    /// <summary>Speed lost per second, in pixels per second.</summary>
    public float Damping;

    /// <summary>Its turn on screen, in radians.</summary>
    public float Angle;

    /// <summary>Its width and height in pixels before the scale curve.</summary>
    public float Size;

    /// <summary>The effect's colour times the burst's tint, before the ramp.</summary>
    public Color Color;

    /// <summary>The cell of the texture strip it shows, or -1 for the soft dot.</summary>
    public int Cell;
}

/// <summary>
/// Turns an <see cref="XpfParticle"/> into the particles one burst throws, the
/// way Godot's particle process material threw them, so every host plays an
/// effect the same: births spread by <see cref="XpfParticle.Explosiveness"/>,
/// lifetimes shortened at random by <see cref="XpfParticle.Randomness"/>,
/// directions inside the spread cone, speeds, damping, scale and angle drawn
/// from their ranges.
/// </summary>
public static class ParticleSpawn
{
    /// <summary>The soft dot and each note cell are this many pixels across at scale 1.</summary>
    public const float TextureSize = 16f;

    /// <summary>
    /// Fills <paramref name="seeds"/> with one burst of <paramref name="effect"/>
    /// at <paramref name="position"/>, its box and direction turned by
    /// <paramref name="angle"/> radians. A non-zero <paramref name="heading"/>
    /// throws the particles in a cone that way instead (screen space).
    /// <paramref name="cells"/> is how many cells the note strip has. Returns
    /// how many seeds were written.
    /// </summary>
    public static int Burst(
        XpfParticle effect,
        Vector2 position,
        Color tint,
        Vector2 area,
        float angle,
        Vector2 heading,
        int cells,
        Random random,
        Span<ParticleSeed> seeds) =>
        Throw(effect, effect.Amount, staggered: true, position, tint, area, angle, heading, cells, random, seeds);

    /// <summary>
    /// <paramref name="count"/> particles of a stream, all born now: a flowing
    /// effect throws <see cref="StreamRate"/> a second, so whoever plays it
    /// calls this each frame with however many came due.
    /// </summary>
    public static int Stream(
        XpfParticle effect,
        int count,
        Vector2 position,
        Color tint,
        float angle,
        int cells,
        Random random,
        Span<ParticleSeed> seeds) =>
        Throw(effect, count, staggered: false, position, tint, default, angle, default, cells, random, seeds);

    /// <summary>Particles a second a flowing effect throws: enough to keep <see cref="XpfParticle.Amount"/> alive.</summary>
    public static float StreamRate(XpfParticle effect) =>
        effect.Lifetime > 0f ? effect.Amount / effect.Lifetime : 0f;

    private static int Throw(
        XpfParticle effect,
        int count,
        bool staggered,
        Vector2 position,
        Color tint,
        Vector2 area,
        float angle,
        Vector2 heading,
        int cells,
        Random random,
        Span<ParticleSeed> seeds)
    {
        int amount = Math.Clamp(count, 0, Math.Min(XpfParticle.MaxAmount, seeds.Length));

        Vector2 way;
        float spread;
        if (heading != Vector2.Zero)
        {
            way = heading.Normalized();
            spread = effect.HeadedSpread;
        }
        else
        {
            way = Vector2.FromAngle(Mathf.DegToRad(effect.Direction)).Rotated(angle);
            spread = effect.Spread;
        }

        Vector2 box = effect.Shape == XpfShape.Box ? (area == Vector2.Zero ? effect.BoxSize : area) : Vector2.Zero;
        float stagger = staggered ? effect.Lifetime * (1f - Math.Clamp(effect.Explosiveness, 0f, 1f)) : 0f;
        float facing = effect.Upright ? 0f : angle;
        Color color = effect.Color * tint;

        for (int i = 0; i < amount; i++)
        {
            ref ParticleSeed seed = ref seeds[i];
            Vector2 offset = new(
                (Next(random) - 0.5f) * box.X,
                (Next(random) - 0.5f) * box.Y);
            seed.Position = position + offset.Rotated(angle);

            float turn = Mathf.DegToRad(spread) * (Next(random) * 2f - 1f);
            seed.Velocity = way.Rotated(turn) * Mathf.Lerp(effect.SpeedMin, effect.SpeedMax, Next(random));

            seed.Delay = amount > 1 ? stagger * i / amount : 0f;
            seed.Lifespan = effect.Lifetime * (1f - Math.Clamp(effect.Randomness, 0f, 1f) * Next(random));
            seed.Damping = Mathf.Lerp(effect.DampingMin, effect.DampingMax, Next(random));
            seed.Angle = facing + Mathf.DegToRad(Mathf.Lerp(effect.AngleMin, effect.AngleMax, Next(random)));
            seed.Size = TextureSize * Mathf.Lerp(effect.ScaleMin, effect.ScaleMax, Next(random));
            seed.Color = color;
            seed.Cell = effect.Texture == XpfTexture.NoteGlyphs && cells > 0
                ? Math.Min(cells - 1, (int)(Next(random) * cells))
                : -1;
        }

        return amount;
    }

    /// <summary>
    /// The scale curve at <paramref name="t"/> (0 to 1 of a particle's life):
    /// 1 when there is no curve, held flat past its ends, and eased between
    /// points as Godot's curve with flat tangents eases them.
    /// </summary>
    public static float Scale(XpfParticle effect, float t)
    {
        var points = effect.ScaleCurve;
        if (points.Count == 0)
            return 1f;
        if (t <= points[0].Time)
            return points[0].Value;

        for (int i = 1; i < points.Count; i++)
        {
            XpfCurvePoint a = points[i - 1], b = points[i];
            if (t > b.Time)
                continue;

            float span = b.Time - a.Time;
            float s = span <= 0f ? 1f : (t - a.Time) / span;
            return Mathf.Lerp(a.Value, b.Value, s * s * (3f - 2f * s));
        }

        return points[^1].Value;
    }

    /// <summary>The colour ramp at <paramref name="t"/>: white with no ramp, linear between stops, held past its ends.</summary>
    public static Color Ramp(XpfParticle effect, float t)
    {
        var stops = effect.Ramp;
        if (stops.Count == 0)
            return Colors.White;
        if (t <= stops[0].Time)
            return stops[0].Color;

        for (int i = 1; i < stops.Count; i++)
        {
            XpfColorStop a = stops[i - 1], b = stops[i];
            if (t > b.Time)
                continue;

            float span = b.Time - a.Time;
            return a.Color.Lerp(b.Color, span <= 0f ? 1f : (t - a.Time) / span);
        }

        return stops[^1].Color;
    }

    private static float Next(Random random) => (float)random.NextDouble();
}

/// <summary>
/// Runs particles for whatever host draws them: bursts are handed over as
/// seeds and attached to a canvas item, which draws them in its place in the
/// tree, moved by the item's transform and faded by its modulate.
/// </summary>
public interface IParticleServer
{
    /// <summary>
    /// Adds <paramref name="seeds"/> of <paramref name="effect"/> to the
    /// particles drawn by canvas item <paramref name="item"/>; note glyphs
    /// show cells of <paramref name="strip"/>, which has <paramref name="cells"/> cells.
    /// </summary>
    void Emit(Handle item, XpfParticle effect, ReadOnlySpan<ParticleSeed> seeds, Handle strip = default, int cells = 1);
}
