using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws grass patches, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    private static readonly List<Vector2> GrassPoints = new();
    private static readonly List<Color> GrassColors = new();

    /// <summary>
    /// Draws one grass patch in one batch of triangles, swaying with
    /// <paramref name="seconds"/>. A selected patch gets an accent box.
    /// </summary>
    public static void DrawGrass(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        float seconds,
        bool selected)
    {
        GrassPatch patch = entity.GetComponent<GrassPatch>();
        SelectItemShader(canvas, entity);
        // Self modulate and group fades reach every blade the same way.
        DrawGrassPatch(
            canvas,
            rid,
            patch,
            view,
            seconds,
            SelfModulate.For(entity, Colors.White),
            entity.TryGetComponent(out GroupColor group) ? group : null);
        canvas.SelectShader(0);

        if (selected)
            DrawTurnedBorder(canvas, rid, patch.Bounds, patch.RotationDegrees, view);
    }

    /// <summary>One patch's blades, multiplied by <paramref name="modulate"/>, then faded by <paramref name="group"/>.</summary>
    public static void DrawGrassPatch(
        IRectCanvas canvas,
        Rid rid,
        GrassPatch patch,
        PlayfieldView view,
        float seconds,
        Color? modulate = null,
        GroupColor? group = null)
    {
        GrassPoints.Clear();
        GrassColors.Clear();
        GrassBlades.Build(patch, seconds, GrassPoints, GrassColors);

        Color tint = modulate ?? Colors.White;
        for (int i = 0; i < GrassPoints.Count; i++)
        {
            GrassPoints[i] = view.PlayfieldToScreen(GrassPoints[i]);
            Color color = GrassColors[i] * tint;
            GrassColors[i] = group is { } fade ? fade.Apply(color) : color;
        }

        canvas.DrawTriangles(
            rid,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(GrassPoints),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(GrassColors));
    }

    /// <summary>The upright box around a grass patch's turned box, on screen.</summary>
    public static Rect2 GrassScreenBounds(GrassPatch patch, PlayfieldView view)
    {
        Rect2 bounds = TurnedBox.Hull(patch.Bounds, patch.RotationDegrees);
        return new Rect2(view.PlayfieldToScreen(bounds.Position), bounds.Size * view.Scale);
    }

    /// <summary>Outlines a turned box on screen, for a selected item that is not upright.</summary>
    public static void DrawTurnedBorder(IRectCanvas canvas, Rid rid, Rect2 box, float degrees, PlayfieldView view)
    {
        Vector2[] corners = TurnedBox.Corners(box, degrees);
        var screen = new Vector2[corners.Length + 1];
        for (int i = 0; i < corners.Length; i++)
            screen[i] = view.PlayfieldToScreen(corners[i]);
        screen[^1] = screen[0];
        canvas.DrawPolyline(rid, screen, BlockSelectedEdge, SelectedEdgeThickness);
    }
}

/// <summary>
/// Lays out a patch's blades as triangles in playfield units. Each blade is a
/// tapering strip of three segments, two quads and a tip, bent further the
/// higher it goes. Where a blade stands, how long it is, its resting lean,
/// its colour and its timing come from a hash of its index and the patch, so
/// they hold still from frame to frame and only the sway moves.
/// </summary>
public static class GrassBlades
{
    /// <summary>Triangles per blade: two per quad, one for the tip.</summary>
    public const int TrianglesPerBlade = 5;

    /// <summary>A blade's width at its base, as a share of its length.</summary>
    private const float Thickness = 0.07f;

    private const float MinHalfWidth = 0.75f;

    /// <summary>How far a blade leans at rest either way, in degrees, whatever the sway.</summary>
    private const float RestLean = 8f;

    /// <summary>How much darker a blade is at its base than at its tip.</summary>
    private const float BaseShade = 0.55f;

    public static void Build(GrassPatch patch, float seconds, List<Vector2> points, List<Color> colors)
    {
        int first = points.Count;
        BuildUpright(patch, seconds, points, colors);
        if (patch.RotationDegrees == 0f)
            return;

        // The patch turns about the centre of its box, as a spike does.
        Vector2 center = patch.Bounds.GetCenter();
        float angle = Mathf.DegToRad(patch.RotationDegrees);
        for (int i = first; i < points.Count; i++)
            points[i] = center + (points[i] - center).Rotated(angle);
    }

    private static void BuildUpright(GrassPatch patch, float seconds, List<Vector2> points, List<Color> colors)
    {
        int count = patch.BladeCount;
        float left = patch.Position.X - patch.Width / 2;
        float spacing = patch.Width / count;
        uint seed = Mix(patch.Seed + 0x85EBCA6Bu);
        Span<Vector2> spine = stackalloc Vector2[4];
        Span<float> half = stackalloc float[4];

        for (int i = 0; i < count; i++)
        {
            uint hash = Mix(seed ^ (uint)i * 0x9E3779B9u);
            float place = Unit(ref hash);
            float tall = Unit(ref hash);
            float rest = Unit(ref hash);
            float phase = Unit(ref hash);
            float give = Unit(ref hash);
            float hue = Unit(ref hash);
            float shade = Unit(ref hash);

            float x = left + (i + 0.5f + (place - 0.5f) * 0.9f) * spacing;
            float length = patch.Length * (0.6f + 0.4f * tall);

            // A slow wave runs along the patch so neighbours move together, with a quicker flutter on top.
            float wave = MathF.Sin(seconds * 1.7f + x * 0.012f + phase * 1.5f) * 0.75f +
                         MathF.Sin(seconds * 3.1f + x * 0.035f + phase * 6.2f) * 0.25f;
            float lean = Mathf.DegToRad((rest - 0.5f) * 2f * RestLean +
                                        patch.Sway * (0.6f + 0.4f * give) * wave);

            float halfWidth = MathF.Max(length * Thickness / 2, MinHalfWidth);
            var root = new Vector2(x, patch.Position.Y);
            spine[0] = root;
            half[0] = halfWidth;
            for (int k = 1; k < 4; k++)
            {
                float f = k / 3f;
                float angle = lean * f;
                spine[k] = spine[k - 1] + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * (length / 3);
                half[k] = halfWidth * (1f - f);
            }

            Color tip = Vary(patch.Color, patch.Variation, hue, shade);
            Color foot = new(tip.R * BaseShade, tip.G * BaseShade, tip.B * BaseShade, tip.A);

            for (int k = 0; k < 3; k++)
            {
                Vector2 across0 = Across(spine, k) * half[k];
                Vector2 across1 = Across(spine, k + 1) * half[k + 1];
                Color c0 = foot.Lerp(tip, k / 3f);
                Color c1 = foot.Lerp(tip, (k + 1) / 3f);
                Vector2 a = spine[k] - across0;
                Vector2 b = spine[k] + across0;

                if (k == 2)
                {
                    Add(points, colors, a, c0, b, c0, spine[3], c1);
                    continue;
                }

                Vector2 c = spine[k + 1] + across1;
                Vector2 d = spine[k + 1] - across1;
                Add(points, colors, a, c0, b, c0, c, c1);
                Add(points, colors, a, c0, c, c1, d, c1);
            }
        }
    }

    /// <summary>
    /// The unit vector across the blade at a spine point, from its segment's
    /// direction; the foot lies flat along the ground whatever the lean.
    /// </summary>
    private static Vector2 Across(ReadOnlySpan<Vector2> spine, int k)
    {
        if (k == 0)
            return Vector2.Right;

        Vector2 along = k < 3 ? spine[k + 1] - spine[k] : spine[k] - spine[k - 1];
        return new Vector2(-along.Y, along.X).Normalized();
    }

    /// <summary>The patch colour with this blade's hue and brightness pushed up to <paramref name="amount"/> either way.</summary>
    private static Color Vary(Color color, float amount, float hue, float shade)
    {
        if (amount <= 0f)
            return color;

        float h = color.H + (hue - 0.5f) * amount * 0.15f;
        float v = Mathf.Clamp(color.V * (1f + (shade - 0.5f) * amount), 0f, 1f);
        return Color.FromHsv(h - MathF.Floor(h), color.S, v, color.A);
    }

    private static void Add(
        List<Vector2> points,
        List<Color> colors,
        Vector2 a, Color ca,
        Vector2 b, Color cb,
        Vector2 c, Color cc)
    {
        points.Add(a);
        points.Add(b);
        points.Add(c);
        colors.Add(ca);
        colors.Add(cb);
        colors.Add(cc);
    }

    private static uint Mix(uint x)
    {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    /// <summary>The next value of a hash chain, from 0 up to 1.</summary>
    private static float Unit(ref uint hash)
    {
        hash = Mix(hash + 0x6D2B79F5u);
        return (hash >> 8) / 16_777_216f;
    }
}
