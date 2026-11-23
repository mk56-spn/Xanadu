using Xanadu.Core.Items;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The orb hold's shape (the ORBS lab, given note timing). An orb waits at its
/// note's position until the head, then crosses at <see cref="Speed"/> while its
/// height follows its path keys, smoothly through them, until its tail. Riding,
/// the body hangs from it (<see cref="Hang"/>); leaving, it keeps the orb's
/// motion at that instant. Everything is a function of song time, so a ride
/// never builds up error.
/// </summary>
public static class Orbs
{
    /// <summary>The shader that draws an orb's ball.</summary>
    public const string Shader = "Assets/Shaders/Items/orb.slang";

    /// <summary>
    /// What an orb's ball looks like: a quad centred on it with room for its glow
    /// and approach ring, drawn by <see cref="Shader"/>. Its colour and state are
    /// set from each orb as it is drawn (<see cref="SetState"/>).
    /// </summary>
    public static Xif Look => _look ??= MakeLook();

    // Made on first use: a static initialiser here would run before Radius is read.
    private static Xif? _look;

    private static Xif MakeLook()
    {
        float room = Radius * 4.4f;
        return new Xif
        {
            Name = "orb",
            Modules =
            {
                new XifGeometry
                {
                    Polygons =
                    {
                        new XifPolygon
                        {
                            Fill = Colors.White,
                            Kind = XifShapeKind.Quad,
                            Points = [new(-room, -room), new(room, -room), new(room, room), new(-room, room)],
                        },
                    },
                },
                new XifShader
                {
                    Shader = XifShaderRef.OfCommon(Shader),
                    Params =
                    {
                        ["orb_radius"] = [Radius],
                        ["orb_tint"] = [1f, 1f, 1f, 1f],
                        ["orb_state"] = [1f, 0f, -1f, 0f],
                    },
                },
            },
        };
    }

    /// <summary>Sets the ball's colour and state for its next draw: how shown it is, lit while ridden, and its approach (1 far, 0 at its head, below 0 none).</summary>
    public static void SetState(Color colour, float alpha, bool lit, float approach)
    {
        XifShader shader = Look.Get<XifShader>()!;
        float[] tint = shader.Params["orb_tint"];
        tint[0] = colour.R;
        tint[1] = colour.G;
        tint[2] = colour.B;
        float[] state = shader.Params["orb_state"];
        state[0] = alpha;
        state[1] = lit ? 1f : 0f;
        state[2] = approach;
    }

    /// <summary>How fast every orb crosses, rightwards: the player's one speed.</summary>
    public const float Speed = PlayRules.CruiseSpeed;

    /// <summary>How long an orb shows before its head, with a ring closing on it, ms.</summary>
    public const long ApproachMs = 800;

    /// <summary>How long an orb lingers, fading, after its tail, ms.</summary>
    public const long LingerMs = 250;

    /// <summary>An orb's radius, playfield units.</summary>
    public const float Radius = 22f;

    /// <summary>The body hangs with its top at the orb.</summary>
    public static Vector2 Hang => new(0f, PlayRules.BodyHeight / 2f);

    /// <summary>
    /// Whether a dash <paramref name="way"/> at <paramref name="nowMs"/> catches
    /// the orb's head: the body already overlapping the head, or the body's
    /// path to the wall that way crossing it. Only the head catches; its trail
    /// does not.
    /// </summary>
    public static bool Catches(in Note orb, double nowMs, Vector2 body, Vector2 way, in Playfield playfield)
    {
        Vector2 head = At(orb, nowMs);
        Vector2 half = PlayRules.BodySize / 2f;
        Vector2 reach = half + new Vector2(Radius);
        if (MathF.Abs(head.X - body.X) <= reach.X && MathF.Abs(head.Y - body.Y) <= reach.Y)
            return true;
        if (way == Vector2.Zero)
            return false;

        // Where a dash that way stops: the field's edges, the floor.
        Vector2 end = way.X > 0 ? body with { X = playfield.Width - half.X }
            : way.X < 0 ? body with { X = half.X }
            : way.Y > 0 ? body with { Y = PlayRules.FloorY(playfield) - half.Y }
            : body with { Y = half.Y };
        Vector2 low = Vector2.Min(body, end) - reach, high = Vector2.Max(body, end) + reach;
        return head.X >= low.X && head.X <= high.X && head.Y >= low.Y && head.Y <= high.Y;
    }

    /// <summary>Where the orb is at a time, held at its start before the head and at its end after the tail.</summary>
    public static Vector2 At(in Note note, double nowMs)
    {
        double t = Math.Clamp(nowMs, note.TimeMs, note.EndMs) - note.TimeMs;
        return note.Position + new Vector2((float)(Speed * t / 1000d), RiseAt(note, t));
    }

    /// <summary>The orb's motion at a time between head and tail, units a second: its fixed speed across, and its height's rate.</summary>
    public static Vector2 Velocity(in Note note, double nowMs)
    {
        const double Step = 4d;
        double t = Math.Clamp(nowMs, note.TimeMs, note.EndMs) - note.TimeMs;
        double a = Math.Max(0d, t - Step), b = Math.Min(note.EndMs - note.TimeMs, t + Step);
        float rise = b > a ? (float)((RiseAt(note, b) - RiseAt(note, a)) / ((b - a) / 1000d)) : 0f;
        return new Vector2(Speed, rise);
    }

    /// <summary>
    /// The height off the start <paramref name="t"/> ms after the head: a
    /// Catmull-Rom curve through the start (0, 0) and the path keys, flat
    /// past the last key.
    /// </summary>
    public static float RiseAt(in Note note, double t)
    {
        OrbKey[]? keys = note.Path;
        if (keys is null || keys.Length == 0 || t <= 0)
            return 0f;

        OrbKey Key(int i) => i < 0 ? new OrbKey(0, 0f) : keys[Math.Min(i, keys.Length - 1)];
        // Index -1 is the start; find the span [k-1, k] holding t.
        int k = 0;
        while (k < keys.Length && keys[k].OffsetMs < t)
            k++;
        if (k >= keys.Length)
            return keys[^1].Rise;

        OrbKey p1 = Key(k - 1), p2 = Key(k);
        OrbKey p0 = k - 2 >= -1 ? Key(k - 2) : p1, p3 = Key(k + 1);
        double span = Math.Max(1d, p2.OffsetMs - p1.OffsetMs);
        float u = (float)((t - p1.OffsetMs) / span);
        // Tangents scaled to the span so uneven key spacing doesn't overshoot.
        float m1 = (p2.Rise - p0.Rise) / (float)Math.Max(1d, p2.OffsetMs - p0.OffsetMs) * (float)span;
        float m2 = (p3.Rise - p1.Rise) / (float)Math.Max(1d, p3.OffsetMs - p1.OffsetMs) * (float)span;
        float u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * p1.Rise + (u3 - 2 * u2 + u) * m1 + (-2 * u3 + 3 * u2) * p2.Rise + (u3 - u2) * m2;
    }

    /// <summary>
    /// The orb an entrance comes out on: starting where the body's top is in
    /// the rift at its time, with the entrance's tail and path.
    /// </summary>
    public static Note OfEntrance(in Note entrance) => new(entrance.TimeMs, entrance.Position - Hang, kind: NoteKind.Orb)
    {
        EndMs = entrance.EndMs,
        Path = entrance.Path,
    };

    /// <summary>The path with a key at <paramref name="offsetMs"/> set to <paramref name="rise"/> (added, or replacing one there), as a new array.</summary>
    public static OrbKey[] WithKey(OrbKey[]? path, long offsetMs, float rise)
    {
        var keys = new System.Collections.Generic.List<OrbKey>(path ?? []);
        keys.RemoveAll(k => k.OffsetMs == offsetMs);
        keys.Add(new OrbKey(offsetMs, rise));
        keys.Sort(static (a, b) => a.OffsetMs.CompareTo(b.OffsetMs));
        return [.. keys];
    }
}
