using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws flares, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>The shader every flare is drawn with; its settings go in as instance values.</summary>
    public const string FlareShaderPath = "Assets/Shaders/flare.slang";

    /// <summary>The corners of a flare's box in shader space, clockwise from the top left.</summary>
    private static readonly Vector2[] FlareCorners =
    {
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };

    private static readonly Vector2[] FlarePoints = new Vector2[6];
    private static readonly Color[] FlareColors = new Color[6];
    private static readonly Vector2[] FlareUvs = new Vector2[6];

    /// <summary>
    /// Draws one flare item: its own shader over its turned box, in its
    /// colour with self modulate and group fades applied. A selected flare
    /// gets an accent box.
    /// </summary>
    public static void DrawFlare(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, bool selected)
    {
        LevelFlare flare = entity.GetComponent<LevelFlare>();
        DrawFlareLight(canvas, rid, flare, view, GroupColor.For(entity, flare.Color), ItemBlend.Of(entity));

        if (selected)
            DrawTurnedBorder(canvas, rid, flare.Bounds, flare.RotationDegrees, view);
    }

    /// <summary>
    /// One flare's light over its box, in <paramref name="tint"/>. Each flare
    /// draws into a run of its own, so its settings reach only it.
    /// </summary>
    public static void DrawFlareLight(
        IRectCanvas canvas,
        Handle rid,
        LevelFlare flare,
        PlayfieldView view,
        Color tint,
        BlendMode blend = BlendMode.Add)
    {
        canvas.SelectCommonShader(FlareShaderPath, blend);
        canvas.NewRun();
        canvas.SetShaderValue("flare_box", new Vector4(flare.Width / 2, flare.Height / 2, flare.Axes, flare.Thickness), 4);
        canvas.SetShaderValue("flare_light", new Vector4(flare.Core, flare.Halo, flare.Glow, flare.Intensity), 4);
        canvas.SetShaderValue("flare_tint", new Vector4(tint.R, tint.G, tint.B, tint.A), 4);
        canvas.SetShaderValue("flare_shape", new Vector4(flare.Falloff, flare.Heat, 0, 0), 2);

        Vector2[] corners = TurnedBox.Corners(flare.Bounds, flare.RotationDegrees);
        ReadOnlySpan<int> order = [0, 1, 2, 0, 2, 3];
        for (int i = 0; i < order.Length; i++)
        {
            FlarePoints[i] = view.PlayfieldToScreen(corners[order[i]]);
            FlareUvs[i] = FlareCorners[order[i]];
            FlareColors[i] = Colors.White;
        }

        canvas.DrawTriangles(rid, FlarePoints, FlareColors, FlareUvs);
        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    /// <summary>The upright box around a flare's turned box, on screen.</summary>
    public static Rect2 FlareScreenBounds(LevelFlare flare, PlayfieldView view)
    {
        Rect2 bounds = TurnedBox.Hull(flare.Bounds, flare.RotationDegrees);
        return new Rect2(view.PlayfieldToScreen(bounds.Position), bounds.Size * view.Scale);
    }
}
