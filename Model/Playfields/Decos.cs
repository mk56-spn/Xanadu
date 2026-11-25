using Xanadu.Core.Items;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Playfields;

/// <summary>
/// Placed Xifs that only look (<see cref="Deco"/>): the box one covers, and
/// the stock ones the editor's tools place. Each stock deco is plain modules,
/// so a mapper's own Xif placed the same way draws the same way.
/// </summary>
public static class Decos
{
    /// <summary>The lens flare effect: a core, a halo and streaks over a square.</summary>
    public const string FlareShader = "Assets/Shaders/Game/flare";

    /// <summary>The box a deco covers, in its own space around its origin: its geometry, spread by its instancer.</summary>
    public static Rect2 LocalBounds(Xif xif)
    {
        Rect2 geometry = XifEditing.Bounds(xif);
        return xif.Get<XifInstancer>() is { } instancer ? XifInstances.Reach(instancer, geometry) : geometry;
    }

    /// <summary>The box a deco covers on the field.</summary>
    public static Rect2 Bounds(in Deco deco)
    {
        Rect2 local = LocalBounds(deco.Xif);
        return new Rect2(local.Position + deco.Position, local.Size);
    }

    /// <summary>
    /// A patch of grass: one tapered blade, rooted at the origin and pointing
    /// up, copied across a strip by an instancer, and a shader stack: the
    /// sway layer bends each copy in the wind out of step, the gradient layer
    /// darkens roots and brightens tips. Its root sits where it is placed, so placed
    /// on the floor's edge it grows out of the floor.
    /// </summary>
    public static Xif Grass() => new()
    {
        Name = "grass",
        Modules =
        {
            new XifGeometry
            {
                Polygons =
                {
                    new XifPolygon
                    {
                        Fill = new Color(0.36f, 0.72f, 0.34f),
                        Points =
                        [
                            new(-4f, 0f), new(-3.6f, -22f), new(-2.6f, -42f), new(-1.2f, -58f), new(0.6f, -70f),
                            new(1.4f, -57f), new(2.6f, -41f), new(3.6f, -21f), new(4f, 0f),
                        ],
                    },
                },
            },
            new XifShader
            {
                Layers = { CommonShaders.SwayLayer, CommonShaders.GradientLayer },
                Params =
                {
                    ["sway_wind"] = [16f, 1f, 0.5f, 0f],
                },
            },
            new XifInstancer { Count = 40, Area = new Vector2(360f, 10f), ScaleJitter = 0.35f, TurnJitter = 0.18f, Seed = 1 },
        },
    };

    /// <summary>A lens flare: a square drawn by <see cref="FlareShader"/>, centred on where it is placed.</summary>
    public static Xif Flare() => new()
    {
        Name = "flare",
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
                        Points = [new(-160f, -160f), new(160f, -160f), new(160f, 160f), new(-160f, 160f)],
                    },
                },
            },
            new XifShader { Shader = XifShaderRef.OfCommon(FlareShader), Fit = true },
        },
    };
}
