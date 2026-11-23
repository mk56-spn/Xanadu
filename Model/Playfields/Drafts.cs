using System.Collections.Generic;
using Xanadu.Core.Items;
using System;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The updraft's shape and pull (<see cref="Draft"/>). Every emitter is the
/// same width; a long stretch is a row of them. In its column (from the
/// emitter up to the field's top) the body's vertical speed eases toward a
/// speed that closes on the hover height, capped both ways, and the hover
/// bobs on the beat. Sideways speed is left alone. All numbers are placeholders.
/// </summary>
public static class Drafts
{
    /// <summary>The shader that draws a draft's look.</summary>
    public const string Shader = "Assets/Shaders/Items/draft.slang";

    private static readonly Dictionary<float, Xif> Looks = new();

    /// <summary>
    /// What a draft of <paramref name="height"/> looks like: a quad from the top
    /// of its column (a quarter past the hover) down to its slab's foot, drawn by
    /// <see cref="Shader"/>; one kept per height. Its <c>draft_state</c> is set
    /// from each draft as it is drawn (<see cref="SetState"/>).
    /// </summary>
    public static Xif LookFor(float height)
    {
        if (Looks.TryGetValue(height, out Xif? known))
            return known;

        float half = Diameter / 2f, reach = height * 1.25f, foot = EmitterHeight / 2f;
        var look = new Xif
        {
            Name = "draft",
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
                            Points = new List<Vector2> { new(-half, -reach), new(half, -reach), new(half, foot), new(-half, foot) },
                        },
                    },
                },
                new XifShader
                {
                    Shader = XifShaderRef.OfCommon(Shader),
                    Params =
                    {
                        ["draft_box"] = [half, reach, EmitterHeight, 0f],
                        ["draft_tint"] = [PlayfieldColors.DraftColor.R, PlayfieldColors.DraftColor.G, PlayfieldColors.DraftColor.B, 1f],
                        ["draft_state"] = [0f, -height, 0.35f, 0f],
                    },
                },
            },
        };
        Looks[height] = look;
        return look;
    }

    /// <summary>Sets a look's state for its next draw: the song's seconds, the hover's height from the vent (up is negative) and its bar's alpha.</summary>
    public static void SetState(Xif look, float seconds, float hover, float bar)
    {
        float[] state = look.Get<XifShader>()!.Params["draft_state"];
        state[0] = seconds;
        state[1] = hover;
        state[2] = bar;
    }

    /// <summary>Every emitter's width, playfield units.</summary>
    public const float Diameter = 360f;

    /// <summary>The emitter's own slab, playfield units high.</summary>
    public const float EmitterHeight = 24f;

    /// <summary>How high a new draft holds the body above its emitter, units.</summary>
    public const float DefaultHeight = 600f;

    /// <summary>How hard the hover pulls: the vertical speed wanted per unit off it, per second.</summary>
    private const float Pull = 5f;

    /// <summary>The fastest the draft lifts or lets the body sink, units a second.</summary>
    private const float MaxRise = 1100f, MaxSink = 700f;

    /// <summary>How quickly the vertical speed eases to the wanted one, per second.</summary>
    private const float Ease = 8f;

    /// <summary>How far the hover bobs up and down with the beat, units.</summary>
    public const float Bob = 28f;

    /// <summary>Whether the body's centre is in the draft's column.</summary>
    public static bool Holds(in Draft draft, Vector2 body) =>
        MathF.Abs(body.X - draft.Position.X) <= Diameter / 2f && body.Y <= draft.Position.Y;

    /// <summary>Where the draft holds the body's centre at a beat: its hover, bobbing.</summary>
    public static float HoverY(in Draft draft, double beat) =>
        draft.Position.Y - draft.Height + Bob * MathF.Sin((float)(beat * Math.Tau));

    /// <summary>The body's vertical speed after <paramref name="delta"/> seconds in the draft.</summary>
    public static float Lift(in Draft draft, float y, float speedY, double beat, float delta)
    {
        float wanted = Math.Clamp((HoverY(draft, beat) - y) * Pull, -MaxRise, MaxSink);
        return speedY + (wanted - speedY) * (1f - MathF.Exp(-Ease * delta));
    }
}
