using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Host.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host.Text;

/// <summary>
/// The host's canvas: <see cref="ServerRectCanvas"/>'s shapes plus text,
/// outlines and glows from <see cref="HostText"/>. Each string becomes one
/// textured triangle array per atlas page on the canvas item, in command
/// order, laid out as <c>GodotRectCanvas</c> lays it out (4 px side padding,
/// the line centred vertically in its box).
/// </summary>
internal sealed class HostRectCanvas(HostText text) : ServerRectCanvas, IRectCanvas
{
    private const int FontSize = 16;
    private const float TextPadding = 4;

    private readonly List<HostText.Placed> _placed = new();

    public override void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Center, FontKind.Regular);

    public override void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Left, FontKind.Regular);

    public override void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Right, FontKind.Regular);

    public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Center, FontKind.Regular);

    public void DrawTextStyledLeft(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Left, FontKind.Regular);

    public void DrawTextStyledRight(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Right, FontKind.Regular);

    public void DrawTextWeighted(Rid canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold) =>
        Draw(canvas, bounds, text, color, fontSize, align, bold ? FontKind.Bold : FontKind.Regular);

    public void DrawTextMono(Rid canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align) =>
        Draw(canvas, bounds, text, color, fontSize, align, FontKind.Mono);

    public void DrawTextOutline(Rid canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold, int size) =>
        Draw(canvas, bounds, text, color, fontSize, align, bold ? FontKind.Bold : FontKind.Regular, outline: size);

    public void DrawTextGlow(
        Rid canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold,
        TextGlow glow, TextOutline? outline = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        FontKind kind = bold ? FontKind.Bold : FontKind.Regular;
        Vector2 origin = Origin(bounds, text, fontSize, align, kind);
        if (glow.Radius > 0 && glow.Color.A > 0)
            Emit(canvas, text, kind, fontSize, origin, glow.Color, glowRadius: glow.Radius, glowIntensity: glow.Intensity);
        if (outline is { } line && line.Size > 0)
            Emit(canvas, text, kind, fontSize, origin, line.Color, outline: line.Size);
        Emit(canvas, text, kind, fontSize, origin, color);
    }

    public void DrawTextTurned(Rid canvas, Rect2 bounds, float degrees, string text, Color color, int fontSize)
    {
        if (degrees == 0f)
        {
            DrawTextStyled(canvas, bounds, text, color, fontSize);
            return;
        }

        // A transform command turns what follows on this item, so the text keeps its place in the draw order.
        Vector2 center = bounds.GetCenter();
        Server.AddSetTransform(canvas, new Transform2D(Mathf.DegToRad(degrees), center));
        DrawTextStyled(canvas, new Rect2(bounds.Position - center, bounds.Size), text, color, fontSize);
        Server.AddSetTransform(canvas, Transform2D.Identity);
    }

    /// <summary>Reads bottom to top, as the Godot canvas drew it: turned a quarter anticlockwise from the bottom-left corner.</summary>
    public void DrawTextVertical(Rid canvas, Rect2 bounds, string text, Color color, int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Server.AddSetTransform(canvas, new Transform2D(-Mathf.Pi / 2, new Vector2(bounds.Position.X + 2, bounds.End.Y - 2)));
        Emit(canvas, text, FontKind.Regular, fontSize, new Vector2(0, fontSize), color);
        Server.AddSetTransform(canvas, Transform2D.Identity);
    }

    private void Draw(
        Rid canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, FontKind kind, float outline = 0)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Emit(canvas, text, kind, fontSize, Origin(bounds, text, fontSize, align, kind), color, outline);
    }

    /// <summary>Where the pen starts, on the baseline: the line centred vertically, the text aligned within the padded width.</summary>
    private Vector2 Origin(Rect2 bounds, string s, int fontSize, TextAlign align, FontKind kind)
    {
        FontFace face = text.Face(kind);
        float baseline = bounds.Position.Y + (bounds.Size.Y - face.Height(fontSize)) / 2 + face.Ascent(fontSize);
        float room = bounds.Size.X - TextPadding * 2;
        float x = align switch
        {
            TextAlign.Right => room - text.Width(s, kind, fontSize),
            TextAlign.Center => (room - text.Width(s, kind, fontSize)) / 2,
            _ => 0,
        };
        return new Vector2(bounds.Position.X + TextPadding + MathF.Max(0, x), baseline);
    }

    private void Emit(
        Rid canvas, string s, FontKind kind, int fontSize, Vector2 origin, Color color,
        float outline = 0, float glowRadius = 0, float glowIntensity = 1)
    {
        text.Place(s, kind, fontSize, origin, _placed, outline, glowRadius, glowIntensity);
        if (_placed.Count == 0)
            return;

        for (int page = 0; page < text.Atlas.PageCount; page++)
        {
            int count = 0;
            foreach (HostText.Placed placed in _placed)
                if (placed.Glyph.Page == page)
                    count++;
            if (count == 0)
                continue;

            var points = new Vector2[count * 4];
            var uvs = new Vector2[count * 4];
            var indices = new int[count * 6];
            int n = 0;
            const float texel = 1f / TextAtlas.Size;
            foreach (HostText.Placed placed in _placed)
            {
                AtlasGlyph g = placed.Glyph;
                if (g.Page != page)
                    continue;

                int v = n * 4;
                points[v] = new Vector2(placed.X, placed.Y);
                points[v + 1] = new Vector2(placed.X + g.Width, placed.Y);
                points[v + 2] = new Vector2(placed.X + g.Width, placed.Y + g.Height);
                points[v + 3] = new Vector2(placed.X, placed.Y + g.Height);
                uvs[v] = new Vector2(g.X * texel, g.Y * texel);
                uvs[v + 1] = new Vector2((g.X + g.Width) * texel, g.Y * texel);
                uvs[v + 2] = new Vector2((g.X + g.Width) * texel, (g.Y + g.Height) * texel);
                uvs[v + 3] = new Vector2(g.X * texel, (g.Y + g.Height) * texel);
                int i = n * 6;
                (indices[i], indices[i + 1], indices[i + 2]) = (v, v + 1, v + 2);
                (indices[i + 3], indices[i + 4], indices[i + 5]) = (v, v + 2, v + 3);
                n++;
            }

            Server.AddTriangleArray(canvas, indices, points, [color], uvs, CanvasTree.ToRid(text.Atlas.TextureId(page)));
        }
    }
}
