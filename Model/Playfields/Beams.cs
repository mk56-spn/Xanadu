using Xanadu.Core.Items;

namespace Xanadu.Model.Playfields;

/// <summary>
/// The player's beam (<see cref="Ecs.Components.HitTrail"/>): what it looks like,
/// a unit quad (0..1 along, -1..1 across) the drawer stretches from the beam's
/// start to just past its end and <see cref="HalfWidth"/> pixels either side,
/// drawn by <see cref="Shader"/> with the beam's length, age and colour set first.
/// </summary>
public static class Beams
{
    /// <summary>The shader that draws a beam.</summary>
    public const string Shader = "Assets/Shaders/Items/beam";

    /// <summary>How far either side of the beam its quad reaches, screen pixels: room for the strike's flash.</summary>
    public const float HalfWidth = 40f;

    /// <summary>How far past its end the quad reaches, pixels: the flash's depth.</summary>
    public const float Past = 4f;

    public static Xif Look { get; } = new()
    {
        Name = "beam",
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
                        Points = [new(0f, -1f), new(1f, -1f), new(1f, 1f), new(0f, 1f)],
                    },
                },
            },
            new XifShader
            {
                Shader = XifShaderRef.OfCommon(Shader),
                Params =
                {
                    ["beam_shape"] = [100f, HalfWidth, 0f, 0f],
                    ["beam_tint"] = [1f, 1f, 1f, 1f],
                },
            },
        },
    };

    /// <summary>Sets the beam for its next draw: its length (pixels), how far through its life (0..1), its colour.</summary>
    public static void SetState(float length, float age, Color colour)
    {
        XifShader shader = Look.Get<XifShader>()!;
        float[] shape = shader.Params["beam_shape"];
        shape[0] = length;
        shape[1] = HalfWidth;
        shape[2] = age;
        float[] tint = shader.Params["beam_tint"];
        tint[0] = colour.R;
        tint[1] = colour.G;
        tint[2] = colour.B;
    }
}
