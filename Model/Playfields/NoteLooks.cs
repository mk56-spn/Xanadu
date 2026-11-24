using System.Collections.Generic;
using Xanadu.Core.Items;

namespace Xanadu.Model.Playfields;

/// <summary>
/// What notes look like, as Xifs the drawer sets a colour on for each draw:
/// <see cref="Key"/>, a key note's rectangle (<see cref="NoteShader"/>), and
/// <see cref="Dash"/>, a dash's bar across the chord with its diamond
/// (<see cref="DashShader"/>). Both are quads centred on the note.
/// </summary>
public static class NoteLooks
{
    public const string NoteShader = "Assets/Shaders/Items/note";
    public const string DashShader = "Assets/Shaders/Items/dash_note";

    /// <summary>The dash's diamond's half diagonal, playfield units.</summary>
    public const float DiamondReach = 14f;

    public static Xif Key => _key ??= Quad("note", NoteShader, Chords.NoteSize / 2f,
        new() { ["note_tint"] = [1f, 1f, 1f, 1f] });

    public static Xif Dash => _dash ??= Quad("dash", DashShader,
        new Vector2(Chords.DashSize.X / 2f + 2f, System.MathF.Max(Chords.DashSize.Y / 2f, DiamondReach + 2f)),
        new() { ["dash_tint"] = [1f, 1f, 1f, 1f], ["dash_shape"] = [Chords.DashSize.X, Chords.DashSize.Y, DiamondReach, 0f] });

    public const string LineShader = "Assets/Shaders/Items/chord_line";
    public const string DotShader = "Assets/Shaders/Items/dot";

    /// <summary>A chord's line: a unit quad (0..1 along, -1..1 across) the drawer stretches over it; <c>line_shape.x</c> is how far along it fades in.</summary>
    public static Xif Line => _line ??= Quad("chord line", LineShader, new Vector2(0.5f, 1f),
        new() { ["line_tint"] = [1f, 1f, 1f, 1f], ["line_shape"] = [0.45f, 0f, 0f, 0f] }, new Vector2(0.5f, 0f));

    /// <summary>A round dot, a unit quad either way of its middle, sized by the drawer.</summary>
    public static Xif Dot => _dot ??= Quad("dot", DotShader, Vector2.One, new() { ["dot_tint"] = [1f, 1f, 1f, 1f] });

    public const string PopShader = "Assets/Shaders/Items/note_pop";
    public const string DashPopShader = "Assets/Shaders/Items/dash_pop";

    /// <summary>A hit note's pop: a unit quad either way of its middle the drawer sizes in pixels to hold the shockwave.</summary>
    public static Xif Pop => _pop ??= Quad("note pop", PopShader, Vector2.One,
        new() { ["pop_tint"] = [1f, 1f, 1f, 1f], ["pop_shape"] = [36f, 50f, 4f, 0f], ["pop_flash"] = [36f, 0f, 0f, 0f] });

    /// <summary>A hit dash's pop: a unit quad either way of its middle the drawer sizes in pixels to hold the bar and sparks.</summary>
    public static Xif DashPop => _dashPop ??= Quad("dash pop", DashPopShader, Vector2.One,
        new() { ["dashpop_shape"] = [150f, 10f, 80f, 10f], ["dashpop_state"] = [1f, 0f, 0f, 0f] });

    private static Xif? _key, _dash, _line, _dot, _pop, _dashPop;

    /// <summary>Sets one of a look's values for its next draw.</summary>
    public static void Set(Xif look, string name, float x, float y = 0f, float z = 0f, float w = 0f)
    {
        float[] value = look.Get<XifShader>()!.Params[name];
        value[0] = x;
        if (value.Length > 1) value[1] = y;
        if (value.Length > 2) value[2] = z;
        if (value.Length > 3) value[3] = w;
    }

    /// <summary>Sets how far along a chord line fades in (0 for solid all along).</summary>
    public static void SetFade(float share) => Line.Get<XifShader>()!.Params["line_shape"][0] = share;

    /// <summary>Sets a look's colour (its first value) for its next draw.</summary>
    public static void SetColour(Xif look, Color colour)
    {
        foreach (KeyValuePair<string, float[]> value in look.Get<XifShader>()!.Params)
        {
            if (!value.Key.EndsWith("_tint"))
                continue;
            value.Value[0] = colour.R;
            value.Value[1] = colour.G;
            value.Value[2] = colour.B;
            value.Value[3] = colour.A;
        }
    }

    private static Xif Quad(string name, string shader, Vector2 half, Dictionary<string, float[]> values, Vector2 middle = default) => new()
    {
        Name = name,
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
                        Points =
                        [
                            middle + new Vector2(-half.X, -half.Y), middle + new Vector2(half.X, -half.Y),
                            middle + new Vector2(half.X, half.Y), middle + new Vector2(-half.X, half.Y),
                        ],
                    },
                },
            },
            new XifShader { Shader = XifShaderRef.OfCommon(shader), Params = values },
        },
    };
}
