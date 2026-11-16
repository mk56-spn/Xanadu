using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Rendering;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

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
        Vector2[] corners = TurnedBox.Corners(flare.Bounds, flare.RotationDegrees);
        for (int i = 0; i < corners.Length; i++)
            corners[i] = view.PlayfieldToScreen(corners[i]);
        DrawFlareQuad(canvas, rid, flare, corners, tint, blend);
    }

    /// <summary>
    /// A flare's light over four screen corners, clockwise from the top left
    /// of its unturned box; its own position and box are not used.
    /// </summary>
    public static void DrawFlareQuad(
        IRectCanvas canvas,
        Handle rid,
        LevelFlare flare,
        ReadOnlySpan<Vector2> screenCorners,
        Color tint,
        BlendMode blend = BlendMode.Add)
    {
        canvas.SelectCommonShader(FlareShaderPath, blend);
        canvas.NewRun();
        canvas.SetShaderValue("flare_box", new Vector4(flare.Width / 2, flare.Height / 2, flare.Axes, flare.Thickness), 4);
        canvas.SetShaderValue("flare_light", new Vector4(flare.Core, flare.Halo, flare.Glow, flare.Intensity), 4);
        canvas.SetShaderValue("flare_tint", new Vector4(tint.R, tint.G, tint.B, tint.A), 4);
        canvas.SetShaderValue("flare_shape", new Vector4(flare.Falloff, flare.Heat, 0, 0), 2);

        ReadOnlySpan<int> order = [0, 1, 2, 0, 2, 3];
        for (int i = 0; i < order.Length; i++)
        {
            FlarePoints[i] = screenCorners[order[i]];
            FlareUvs[i] = FlareCorners[order[i]];
            FlareColors[i] = Colors.White;
        }

        canvas.DrawTriangles(rid, FlarePoints, FlareColors, FlareUvs);
        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    /// <summary>How long and how tall a dash flare's box is at its fullest, in screen pixels.</summary>
    public static readonly Vector2 DashFlareSize = new(520f, 300f);

    /// <summary>
    /// A dash flare at its age: the box swells fast to full size and the
    /// light flashes past full brightness, then both settle as it fades.
    /// </summary>
    public static void DrawDashFlare(IRectCanvas canvas, Handle rid, Entity entity)
    {
        DashFlare burst = entity.GetComponent<DashFlare>();
        float t = Math.Clamp(burst.Age / Math.Max(burst.Lifetime, 0.001f), 0f, 1f);
        float swell = 1f - (1f - t) * (1f - t) * (1f - t);
        float fade = (1f - t) * (1f - t);
        Vector2 size = DashFlareSize * (0.35f + 0.85f * swell);

        LevelFlare flare = new()
        {
            Width = size.X,
            Height = size.Y,
            Axes = 2,
            Thickness = 0.06f,
            Falloff = 1.2f,
            Core = 0.22f * (1f - 0.5f * t),
            Halo = 0.8f,
            Glow = 0.45f,
            Intensity = 2.4f * fade,
            Heat = 0.85f,
        };

        Span<Vector2> corners = stackalloc Vector2[4];
        Vector2 along = Vector2.UnitX.Rotated(Mathf.DegToRad(burst.AngleDegrees));
        Vector2 across = new(-along.Y, along.X);
        Vector2 half = size / 2;
        corners[0] = burst.Position - along * half.X - across * half.Y;
        corners[1] = burst.Position + along * half.X - across * half.Y;
        corners[2] = burst.Position + along * half.X + across * half.Y;
        corners[3] = burst.Position - along * half.X + across * half.Y;
        DrawFlareQuad(canvas, rid, flare, corners, burst.Color);
    }

}
