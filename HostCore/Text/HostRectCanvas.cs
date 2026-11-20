using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Host.Rendering;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Text;

/// <summary>
/// The host's canvas: <see cref="ServerRectCanvas"/>'s shapes plus text,
/// outlines and glows from <see cref="HostText"/>. Each glyph becomes one
/// textured primitive on the canvas item, in command order (4 px side padding,
/// the line centred vertically in its box). Shape glows come from
/// <see cref="HostShapeGlow"/>.
/// </summary>
internal sealed class HostRectCanvas(HostText text) : ServerRectCanvas, IRectCanvas
{
    private static CanvasTree Tree => (CanvasTree)Server;

    private const int FontSize = 16;
    private const float TextPadding = 4;

    private readonly List<HostText.Placed> _placed = new();
    private HostShapeGlow? _shapeGlow;

    public void DrawShapeGlow(Handle canvas, ReadOnlySpan<Vector2> points, bool closed, bool filled, float strokeWidth, ShapeGlow glow) =>
        (_shapeGlow ??= new HostShapeGlow(Server)).Draw(canvas, points, closed, filled, strokeWidth, glow);

    public override void DrawText(Handle canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Center, FontKind.Regular);

    public override void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Left, FontKind.Regular);

    public override void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color) =>
        Draw(canvas, bounds, text, color, FontSize, TextAlign.Right, FontKind.Regular);

    public void DrawTextStyled(Handle canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Center, FontKind.Regular);

    public void DrawTextStyledLeft(Handle canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Left, FontKind.Regular);

    public void DrawTextStyledRight(Handle canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        Draw(canvas, bounds, text, color, fontSize, TextAlign.Right, FontKind.Regular);

    public void DrawTextWeighted(Handle canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold) =>
        Draw(canvas, bounds, text, color, fontSize, align, bold ? FontKind.Bold : FontKind.Regular);

    public void DrawTextMono(Handle canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align) =>
        Draw(canvas, bounds, text, color, fontSize, align, FontKind.Mono);

    public void DrawTextOutline(Handle canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold, int size) =>
        Draw(canvas, bounds, text, color, fontSize, align, bold ? FontKind.Bold : FontKind.Regular, outline: size);

    public void DrawTextGlow(
        Handle canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, bool bold,
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

    public void DrawTextTurned(Handle canvas, Rect2 bounds, float degrees, string text, Color color, int fontSize)
    {
        if (degrees == 0f)
        {
            DrawTextStyled(canvas, bounds, text, color, fontSize);
            return;
        }

        // A transform command turns what follows on this item, so the text keeps its place in the draw order.
        Vector2 center = bounds.GetCenter();
        Server.AddSetTransform(canvas, new Transform2D(float.DegreesToRadians(degrees), center));
        DrawTextStyled(canvas, new Rect2(bounds.Position - center, bounds.Size), text, color, fontSize);
        Server.AddSetTransform(canvas, Transform2D.Identity);
    }

    /// <summary>Reads bottom to top, as the game has always drawn it: turned a quarter anticlockwise from the bottom-left corner.</summary>
    public void DrawTextVertical(Handle canvas, Rect2 bounds, string text, Color color, int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Server.AddSetTransform(canvas, new Transform2D(-float.Pi / 2, new Vector2(bounds.Position.X + 2, bounds.End.Y - 2)));
        Emit(canvas, text, FontKind.Regular, fontSize, new Vector2(0, fontSize), color);
        Server.AddSetTransform(canvas, Transform2D.Identity);
    }

    private void Draw(
        Handle canvas, Rect2 bounds, string text, Color color, int fontSize, TextAlign align, FontKind kind, float outline = 0)
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
        Handle canvas, string s, FontKind kind, int fontSize, Vector2 origin, Color color,
        float outline = 0, float glowRadius = 0, float glowIntensity = 1)
    {
        text.Place(s, kind, fontSize, origin, _placed, outline, glowRadius, glowIntensity);
        if (_placed.Count == 0)
            return;

        Route(canvas);
        // One textured primitive a glyph, straight from its atlas cell.
        const float texel = 1f / TextAtlas.Size;
        foreach (HostText.Placed placed in _placed)
        {
            AtlasGlyph g = placed.Glyph;
            var rect = new Rect2(placed.X, placed.Y, g.Width, g.Height);
            var uv = new Rect2(g.X * texel, g.Y * texel, g.Width * texel, g.Height * texel);
            Tree.AddTextureRegion(canvas, rect, uv, color, CanvasTree.ToHandle(text.Atlas.TextureId(g.Page)));
        }
    }
}
