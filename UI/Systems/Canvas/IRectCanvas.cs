using Xanadu.Core.Rendering;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    /// <summary>
    /// Routes the draws that follow through a custom shader slot, 1-based, or
    /// none for 0, and with a blend mode. Canvases with no shaders ignore it.
    /// The blend lasts until the next selection, like the shader.
    /// <paramref name="sourceGroup"/> is the group whose item-shader track the
    /// draws follow, or -1 for none; runs keep separate from other sources.
    /// Selecting also clears the instance values set since the last selection.
    /// </summary>
    void SelectShader(int slot, BlendMode blend = BlendMode.Mix, int sourceGroup = -1) { }

    /// <summary>
    /// Sets an instance uniform for the draws that follow, on the run they go
    /// into (Docs/render-architecture.md §5.3). Values are written when the run
    /// is picked; canvases with no shaders ignore it.
    /// </summary>
    void SetShaderValue(string param, Vector4 value, int size) { }

    /// <summary>
    /// Routes the draws that follow through a common shader, by resource
    /// path, or none for an empty path, and with a blend mode. Canvases with
    /// no shaders ignore it.
    /// </summary>
    void SelectCommonShader(string path, BlendMode blend = BlendMode.Mix) { }

    /// <summary>
    /// Sends the draws that follow into a run of their own, even when the
    /// selection matches the current run's, so instance values set for them
    /// reach only them. Canvases with no shaders ignore it.
    /// </summary>
    void NewRun() { }








    void SetVisible(Handle canvas, bool visible);

    void SetTransform(Handle canvas, Transform2D transform);

    void Clear(Handle canvas);

    void DrawRect(Handle canvas, Rect2 bounds, Color color);

    /// <summary>Draws a texture, by its canvas texture id, into a rectangle.</summary>
    void DrawTexture(Handle canvas, Rect2 bounds, Handle texture) { }

    /// <summary>Draws a filled circle.</summary>
    void DrawCircle(Handle canvas, Vector2 center, float radius, Color color) { }

    /// <summary>Fills a convex polygon.</summary>
    void DrawPolygon(Handle canvas, ReadOnlySpan<Vector2> points, Color color) { }

    /// <summary>
    /// Fills a batch of triangles in one draw: every three points are one
    /// triangle, and each point has its own colour, blended across the face.
    /// </summary>
    void DrawTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors) { }

    /// <summary>
    /// The same, with a UV per point for the shader drawing them to read;
    /// canvases without shaders draw the triangles as they would without.
    /// </summary>
    void DrawTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs) =>
        DrawTriangles(canvas, points, colors);

    /// <summary>
    /// The same, with a curve tag per triangle (<see cref="CanvasCurves"/>):
    /// a tagged triangle's corners are a quadratic Bézier's start, control and
    /// end, and the GPU fills it only up to the curve. Canvases that cannot
    /// trim draw the straight chord instead (see <see cref="CanvasCurves.Straighten"/>).
    /// </summary>
    void DrawCurveTriangles(Handle canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs, ReadOnlySpan<byte> curves)
    {
        (Vector2[] kept, Color[] keptColors, Vector2[] keptUvs) = CanvasCurves.Straighten(points, colors, uvs, curves);
        DrawTriangles(canvas, kept, keptColors, keptUvs);
    }

    /// <summary>Fills a rectangle with a smooth ramp between two colours, along its width or its height.</summary>
    void DrawGradientRect(Handle canvas, Rect2 bounds, Color start, Color end, bool horizontal) { }

    /// <summary>Draws a connected polyline.</summary>
    void DrawPolyline(
        Handle canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true)
    {
        for (int i = 1; i < points.Length; i++)
            DrawLine(canvas, points[i - 1], points[i], color, width);
    }

    /// <summary>Draws many one-pixel rectangle outlines of one colour, as one batch where the canvas can.</summary>
    void DrawRectOutlines(Handle canvas, ReadOnlySpan<Rect2> rects, Color color)
    {
        foreach (Rect2 rect in rects)
        {
            Vector2 topRight = new(rect.End.X, rect.Position.Y);
            Vector2 bottomLeft = new(rect.Position.X, rect.End.Y);
            DrawLine(canvas, rect.Position, topRight, color, 1);
            DrawLine(canvas, topRight, rect.End, color, 1);
            DrawLine(canvas, rect.End, bottomLeft, color, 1);
            DrawLine(canvas, bottomLeft, rect.Position, color, 1);
        }
    }

    /// <summary>Draws an outlined circle as one connected polyline.</summary>
    void DrawCircleOutline(
        Handle canvas,
        Vector2 center,
        float radius,
        Color color,
        float width,
        int segments = 40)
    {
        if (segments < 3 || radius <= 0)
            return;

        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = float.Tau * i / segments;
            points[i] = center + new Vector2(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius);
        }

        DrawPolyline(canvas, points, color, width);
    }

    void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width);

    void DrawText(Handle canvas, Rect2 bounds, string text, Color color);

    /// <summary>Draws centered text with an explicit font size.</summary>
    void DrawTextStyled(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawText(canvas, bounds, text, color);

    void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color);

    void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color);

    /// <summary>
    /// Draws text centred in <paramref name="bounds"/>, the whole box turned
    /// about its centre by <paramref name="degrees"/> clockwise. Canvases that
    /// cannot turn text draw it upright.
    /// </summary>
    void DrawTextTurned(Handle canvas, Rect2 bounds, float degrees, string text, Color color, int fontSize) =>
        DrawTextStyled(canvas, bounds, text, color, fontSize);

    /// <summary>Draws vertically oriented text.</summary>
    void DrawTextVertical(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float cellHeight = bounds.Size.Y / text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            DrawTextStyledLeft(
                canvas,
                new Rect2(
                    bounds.Position.X,
                    bounds.Position.Y + cellHeight * i,
                    bounds.Size.X,
                    cellHeight),
                text[i].ToString(),
                color,
                fontSize);
        }
    }

    /// <summary>Draws left-aligned text with an explicit font size.</summary>
    void DrawTextStyledLeft(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextStyled(canvas, bounds, text, color, fontSize);

    /// <summary>Draws right-aligned text with an explicit font size.</summary>
    void DrawTextStyledRight(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextStyled(canvas, bounds, text, color, fontSize);

    /// <summary>
    /// Draws text with an explicit size, alignment and weight. Canvases with
    /// no bold face draw it at regular weight.
    /// </summary>
    void DrawTextWeighted(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold)
    {
        switch (align)
        {
            case TextAlign.Left:
                DrawTextStyledLeft(canvas, bounds, text, color, fontSize);
                break;
            case TextAlign.Right:
                DrawTextStyledRight(canvas, bounds, text, color, fontSize);
                break;
            default:
                DrawTextStyled(canvas, bounds, text, color, fontSize);
                break;
        }
    }

    /// <summary>
    /// Draws text in the monospaced face. Canvases without one draw it in the
    /// regular face.
    /// </summary>
    void DrawTextMono(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align) =>
        DrawTextWeighted(canvas, bounds, text, color, fontSize, align, bold: false);

    /// <summary>
    /// Draws text with a glow behind it. Canvases that cannot blur draw the
    /// text without the glow.
    /// </summary>
    void DrawTextGlow(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        Glow glow,
        TextOutline? outline = null)
    {
        if (outline is { } line)
            DrawTextOutline(canvas, bounds, text, line.Color, fontSize, align, bold, line.Size);

        DrawTextWeighted(canvas, bounds, text, color, fontSize, align, bold);
    }

    /// <summary>
    /// Draws a glow behind a shape's outline. Canvases that cannot blur draw nothing.
    /// </summary>
    void DrawShapeGlow(
        Handle canvas,
        ReadOnlySpan<Vector2> points,
        bool closed,
        bool filled,
        float strokeWidth,
        Glow glow)
    {
    }

    /// <summary>Draws just the outline of text; canvases that cannot outline draw nothing.</summary>
    void DrawTextOutline(
        Handle canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        int size)
    {
    }
}
