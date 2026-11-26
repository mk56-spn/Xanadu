using System.Collections.Generic;
using Xanadu.Core.Items;

namespace Xanadu.Model.Playfields;

/// <summary>
/// What a hit note's pops look like, as Xifs the drawer sets values on for each draw
/// (<see cref="Pop"/>, <see cref="DashPop"/>). The notes themselves are drawn by the
/// notes stack (Drawers' <c>NoteCopies</c>), not as Xifs.
/// </summary>
public static class NoteLooks
{
    /// <summary>The dash's diamond's half diagonal, playfield units.</summary>
    public const float DiamondReach = 14f;

    public const string PopShader = "Assets/Shaders/Game/note_pop";
    public const string DashPopShader = "Assets/Shaders/Game/dash_pop";

    /// <summary>A hit note's pop: a unit quad either way of its middle the drawer sizes in pixels to hold the shockwave.</summary>
    public static Xif Pop => _pop ??= Quad("note pop", PopShader, Vector2.One,
        new() { ["pop_tint"] = [1f, 1f, 1f, 1f], ["pop_shape"] = [36f, 50f, 4f, 0f], ["pop_flash"] = [36f, 0f, 0f, 0f] });

    /// <summary>A hit dash's pop: a unit quad either way of its middle the drawer sizes in pixels to hold the bar and sparks.</summary>
    public static Xif DashPop => _dashPop ??= Quad("dash pop", DashPopShader, Vector2.One,
        new() { ["dashpop_shape"] = [150f, 10f, 80f, 10f], ["dashpop_state"] = [1f, 0f, 0f, 0f] });

    private static Xif? _pop, _dashPop;

    /// <summary>Sets one of a look's values for its next draw.</summary>
    public static void Set(Xif look, string name, float x, float y = 0f, float z = 0f, float w = 0f)
    {
        float[] value = look.Get<XifShader>()!.Params[name];
        value[0] = x;
        if (value.Length > 1) value[1] = y;
        if (value.Length > 2) value[2] = z;
        if (value.Length > 3) value[3] = w;
    }

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

    private static Xif Quad(string name, string shader, Vector2 half, Dictionary<string, float[]> values) => new()
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
                            new Vector2(-half.X, -half.Y), new Vector2(half.X, -half.Y),
                            new Vector2(half.X, half.Y), new Vector2(-half.X, half.Y),
                        ],
                    },
                },
            },
            new XifShader { Shader = XifShaderRef.OfCommon(shader), Params = values },
        },
    };
}
