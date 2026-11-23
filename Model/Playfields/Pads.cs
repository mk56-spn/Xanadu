using System.Collections.Generic;
using Xanadu.Core.Items;
using System;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>A pad's shape and its launch (<see cref="Pad"/>).</summary>
public static class Pads
{
    /// <summary>The shader that draws a pad's look.</summary>
    public const string Shader = "Assets/Shaders/Items/pad.slang";

    /// <summary>
    /// What every pad looks like: a quad centred on it with room above for the
    /// launch column and round it for the timing rings, drawn by <see cref="Shader"/>.
    /// Its <c>pad_state</c> (spring, approach, editor ring) is set from each pad as
    /// it is drawn (<see cref="SetState"/>).
    /// </summary>
    public static Xif Look => _look ??= MakeLook();

    // Made on first use: a static initialiser here would run before Size's.
    private static Xif? _look;

    private static Xif MakeLook()
    {
        Vector2 half = Size / 2f;
        var room = new Vector2(half.X * 3.4f, half.Y + half.X * 4f);
        return new Xif
        {
            Name = "pad",
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
                            Points = new List<Vector2> { new(-room.X, -room.Y), new(room.X, -room.Y), new(room.X, room.Y), new(-room.X, room.Y) },
                        },
                    },
                },
                new XifShader
                {
                    Shader = XifShaderRef.OfCommon(Shader),
                    Params =
                    {
                        ["pad_half"] = [half.X, half.Y],
                        ["pad_tint"] = [PlayfieldColors.PadColor.R, PlayfieldColors.PadColor.G, PlayfieldColors.PadColor.B, 1f],
                        ["pad_state"] = [0f, -1f, 0f, 0f],
                    },
                },
            },
        };
    }

    /// <summary>
    /// Sets the look's state for the next draw: how far through its spring it is
    /// (1 as it fires, to 0), where a timed pad's approach is (1 far, 0 at its time,
    /// below 0 none) and the editor's timed-pad ring (its alpha, 0 none).
    /// </summary>
    public static void SetState(float spring, float approach, float ring)
    {
        float[] state = Look.Get<XifShader>()!.Params["pad_state"];
        state[0] = spring;
        state[1] = approach;
        state[2] = ring;
    }

    /// <summary>A pad's size: a low slab, wider than the body. Playfield units.</summary>
    public static readonly Vector2 Size = new(150f, 28f);

    /// <summary>The upward speed a new pad launches at, units a second: a little over the height of the field's middle under gravity. A placeholder.</summary>
    public const float DefaultSpeed = 1500f;

    /// <summary>How long the pad's spring lasts after it fires, ms.</summary>
    public const double SpringMs = 220;

    /// <summary>How long a timed pad shows a ring closing on it before its time, ms.</summary>
    public const long ApproachMs = 600;

    /// <summary>
    /// How far a timed pad reaches for the body at its time: this far either
    /// side, and from this far above down to standing on it. Farther, it doesn't fire.
    /// </summary>
    public static readonly Vector2 Reach = new(260f, 700f);

    /// <summary>Where the body's centre is when it stands on the pad.</summary>
    public static Vector2 StandingOn(in Pad pad) =>
        new(pad.Position.X, pad.Position.Y - Size.Y / 2f - PlayRules.BodyHeight / 2f);

    /// <summary>Whether a timed pad reaches the body, centred at <paramref name="body"/>, at its time.</summary>
    public static bool Reaches(in Pad pad, Vector2 body)
    {
        Vector2 gap = body - StandingOn(pad);
        return MathF.Abs(gap.X) <= Reach.X && gap.Y >= -Reach.Y && gap.Y <= Size.Y + PlayRules.BodyHeight / 2f;
    }

    /// <summary>Whether the body, centred at <paramref name="body"/>, touches the pad.</summary>
    public static bool Touches(in Pad pad, Vector2 body)
    {
        Vector2 reach = (Size + PlayRules.BodySize) / 2f;
        Vector2 gap = body - pad.Position;
        return MathF.Abs(gap.X) < reach.X && MathF.Abs(gap.Y) < reach.Y;
    }
}
