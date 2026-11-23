using System.Collections.Generic;

namespace Xanadu.Core.Items;

/// <summary>
/// Makes many copies of its node's geometry: one blade becomes a patch of
/// grass. Copies are spread over <see cref="Area"/>, each turned and sized a
/// little differently, all from <see cref="Seed"/>, so the same module always
/// makes the same copies. They are placed once and never die; anything that
/// moves them over time is the shader's (a function of time and the copy's
/// number) or, later, a Simulation module's. See Docs/xif-modules.md.
/// </summary>
public sealed class XifInstancer : XifModule
{
    /// <summary>The most copies one instancer makes.</summary>
    public const int MaxCount = 4096;

    /// <summary>How many copies.</summary>
    public int Count { get; set; } = 32;

    /// <summary>The patch the copies' origins spread over, centred on the node, in the node's own units.</summary>
    public Vector2 Area { get; set; } = new(320f, 0f);

    /// <summary>How much a copy's size varies, as a share either way: 0.3 makes copies 0.7 to 1.3 times as big.</summary>
    public float ScaleJitter { get; set; } = 0.3f;

    /// <summary>How far a copy may be turned either way, radians.</summary>
    public float TurnJitter { get; set; } = 0.15f;

    /// <summary>A new seed is a new scatter of the same copies.</summary>
    public uint Seed { get; set; } = 1;

    public override XifModule Copy() => new XifInstancer
    {
        Count = Count, Area = Area, ScaleJitter = ScaleJitter, TurnJitter = TurnJitter, Seed = Seed,
    };

    public override bool Same(XifModule other) =>
        other is XifInstancer o && o.Count == Count && o.Area == Area && o.ScaleJitter == ScaleJitter &&
        o.TurnJitter == TurnJitter && o.Seed == Seed;
}

/// <summary>
/// One copy an <see cref="XifInstancer"/> makes: its number (which the shader
/// gets, to seed per-copy looks), where its origin sits from the node's, its
/// size and its turn.
/// </summary>
public readonly record struct XifInstance(int Index, Vector2 Offset, float Scale, float Turn)
{
    /// <summary>A geometry point of this copy, in the node's own space.</summary>
    public Vector2 Place(Vector2 point)
    {
        float sin = MathF.Sin(Turn), cos = MathF.Cos(Turn);
        Vector2 scaled = point * Scale;
        return Offset + new Vector2(scaled.X * cos - scaled.Y * sin, scaled.X * sin + scaled.Y * cos);
    }
}

/// <summary>Works out an instancer's copies; the same module always gives the same ones, on any machine.</summary>
public static class XifInstances
{
    /// <summary>
    /// Fills <paramref name="into"/> with the copies, back to front (the ones
    /// higher up the patch first, so nearer ones cover them). Origins spread
    /// evenly across the width, each nudged inside its own share, so a patch
    /// never bunches up or leaves a gap.
    /// </summary>
    public static void Place(XifInstancer instancer, List<XifInstance> into)
    {
        into.Clear();
        int count = Math.Clamp(instancer.Count, 0, XifInstancer.MaxCount);
        Vector2 area = instancer.Area;
        for (int i = 0; i < count; i++)
        {
            float across = (i + Unit(instancer.Seed, i, 1)) / count;
            float deep = Unit(instancer.Seed, i, 2);
            var offset = new Vector2((across - 0.5f) * area.X, (deep - 0.5f) * area.Y);
            float scale = 1f + (Unit(instancer.Seed, i, 3) * 2f - 1f) * instancer.ScaleJitter;
            float turn = (Unit(instancer.Seed, i, 4) * 2f - 1f) * instancer.TurnJitter;
            into.Add(new XifInstance(i, offset, MathF.Max(0.05f, scale), turn));
        }
        into.Sort(static (a, b) => a.Offset.Y != b.Offset.Y ? a.Offset.Y.CompareTo(b.Offset.Y) : a.Index.CompareTo(b.Index));
    }

    /// <summary>The box every copy's origin and its geometry can reach, in the node's own space.</summary>
    public static Rect2 Reach(XifInstancer instancer, Rect2 geometry)
    {
        float grow = 1f + MathF.Max(0f, instancer.ScaleJitter);
        var scaled = new Rect2(geometry.Position * grow, geometry.Size * grow);
        return new Rect2(scaled.Position - instancer.Area / 2f, scaled.Size + instancer.Area);
    }

    /// <summary>0..1 from a seed, a copy and which value of it; a small integer hash (PCG's output step).</summary>
    private static float Unit(uint seed, int index, uint salt)
    {
        uint state = seed * 747796405u + (uint)index * 2891336453u + salt * 0x9E3779B9u;
        state = state * 747796405u + 2891336453u;
        uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
        word = (word >> 22) ^ word;
        return (word >> 8) * (1f / 16777216f);
    }
}
