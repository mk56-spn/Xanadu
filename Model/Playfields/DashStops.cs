using Xanadu.Core.Items;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>The dash ender's shape: one fixed square, and whether the body's centre is in it.</summary>
public static class DashStops
{
    /// <summary>The shader that draws a dash ender's look.</summary>
    public const string Shader = "Assets/Shaders/Game/dash_stop";

    /// <summary>
    /// What every dash ender looks like: a quad with room round the block for
    /// its glow, drawn by <see cref="Shader"/> with the block's size and the
    /// stop colour. Centred on the ender's place; drawn by XifDrawer.
    /// </summary>
    public static Xif Look => _look ??= MakeLook();

    // Made on first use: a static initialiser here would run before Size's.
    private static Xif? _look;

    private static Xif MakeLook()
    {
        Vector2 half = Size / 2f;
        Vector2 room = half * 1.55f;
        return new Xif
        {
            Name = "dash stop",
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
                            Points = [new(-room.X, -room.Y), new(room.X, -room.Y), new(room.X, room.Y), new(-room.X, room.Y)],
                        },
                    },
                },
                new XifShader
                {
                    Shader = XifShaderRef.OfCommon(Shader),
                    Params =
                    {
                        ["stop_half"] = [half.X, half.Y],
                        // Border, bars, and the glow's reach out to the quad's edge.
                        ["stop_lines"] = [3f, 4f, room.X - half.X],
                        ["stop_tint"] = [PlayfieldColors.DashStopColor.R, PlayfieldColors.DashStopColor.G, PlayfieldColors.DashStopColor.B, 1f],
                    },
                },
            },
        };
    }

    /// <summary>The marker's size, playfield units.</summary>
    public static readonly Vector2 Size = new(110f, 110f);

    /// <summary>Whether the body's centre, at <paramref name="body"/>, is in the marker.</summary>
    public static bool Holds(in DashStop stop, Vector2 body) =>
        MathF.Abs(body.X - stop.Position.X) <= Size.X / 2f && MathF.Abs(body.Y - stop.Position.Y) <= Size.Y / 2f;

    /// <summary>Where a dash <paramref name="way"/> that entered the marker stops: on its centre along the dash's line.</summary>
    public static Vector2 StopAt(in DashStop stop, Vector2 body, Vector2 way) =>
        MathF.Abs(way.X) > MathF.Abs(way.Y) ? body with { X = stop.Position.X } : body with { Y = stop.Position.Y };
}
