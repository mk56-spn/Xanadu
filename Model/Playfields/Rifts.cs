using System.Collections.Generic;
using Xanadu.Core.Items;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The entrance's rift, as Xifs the drawer sets numbers on each frame: <see cref="Look"/>,
/// a square round the rift drawn by <see cref="Shader"/> (glow, motes, beat ring,
/// seam, count pips, dash-exit arrow, and the release's bloom, shockwaves, sparks
/// and snapping seam), and <see cref="FlareLook"/>, a unit quad the drawer
/// stretches over the release's column of light, drawn by <see cref="FlareShader"/>.
/// </summary>
public static class Rifts
{
    public const string Shader = "Assets/Shaders/Items/rift.slang";
    public const string FlareShader = "Assets/Shaders/Items/rift_flare.slang";

    /// <summary>The seam's half height and half the body's width, playfield units.</summary>
    public static float HalfHeight => PlayRules.BodyHeight * 0.8f;

    public static float BodyHalf => PlayRules.BodyWidth / 2f;

    public static Xif Look => _look ??= Quad("rift", Shader, HalfHeight * 4.8f, new()
    {
        ["rift_size"] = [BodyHalf, HalfHeight],
        ["rift_glow"] = [1f, 1f, 0f, 0f],
        ["rift_lens"] = [0f, 0f, 0f, 0f],
        ["rift_ring"] = [0f, 2f, 0f, 0f],
        ["rift_motes"] = [0f, 0f, 0f, 0f],
        ["rift_pips"] = [4f, 0f, 22f, 6f],
        ["rift_exit"] = [0f, 0f, 0f, 0f],
        ["rift_release"] = [0f, 0f, 0f, 0f],
    });

    public static Xif FlareLook => _flare ??= Quad("rift flare", FlareShader, 0.5f, new() { ["rift_flare"] = [1f, 0f, 0f, 0f] }, centred: false);

    private static Xif? _look, _flare;

    /// <summary>Sets one of a look's values for its next draw.</summary>
    public static void Set(Xif look, string name, float x, float y = 0f, float z = 0f, float w = 0f)
    {
        float[] value = look.Get<XifShader>()!.Params[name];
        value[0] = x;
        if (value.Length > 1) value[1] = y;
        if (value.Length > 2) value[2] = z;
        if (value.Length > 3) value[3] = w;
    }

    /// <summary>A quad <paramref name="half"/> either way of its origin (or 0..1 both ways), shaded.</summary>
    private static Xif Quad(string name, string shader, float half, Dictionary<string, float[]> values, bool centred = true)
    {
        float low = centred ? -half : 0f, high = centred ? half : 1f;
        return new Xif
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
                            Points = new List<Vector2> { new(low, low), new(high, low), new(high, high), new(low, high) },
                        },
                    },
                },
                new XifShader { Shader = XifShaderRef.OfCommon(shader), Params = values },
            },
        };
    }
}
