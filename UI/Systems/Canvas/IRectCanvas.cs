using System;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Canvas;

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
    /// into (docs/render-architecture.md §5.3). Values are written when the run
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

    /// <summary>
    /// Routes the draws that follow into draw layer <paramref name="layer"/>'s
    /// own container, which stacks over everything drawn before it. Layers are
    /// begun back to front, each once per frame. Canvases with no layers ignore it.
    /// <paramref name="target"/> is the canvas item to put the container under,
    /// for a self-contained layer's own viewport; null is the canvas itself.
    /// </summary>
    void BeginLayer(int layer, Handle? target = null) { }

    /// <summary>
    /// Makes the next shaded run copy the whole screen to the back buffer just
    /// before it draws, so a screen-reading pass sees everything drawn so far
    /// (docs/render-architecture.md D9/D10). Canvases with no shaders ignore it.
    /// </summary>
    void CopyScreen() { }

    /// <summary>
    /// Refreshes the back buffer for one rect now, where the draw order reaches
    /// it: a screen-read region (docs/render-architecture.md step 11, D10). The
    /// rect is in the canvas's own coordinates; canvases with no shaders ignore it.
    /// </summary>
    void CopyScreen(Rect2 bounds) { }

    /// <summary>
    /// Starts a buffered group (D12): the draws that follow go into one image,
    /// composited when it ends with the buffer's tint, alpha, shader and blend.
    /// Canvases with no buffers ignore it.
    /// </summary>
    void BeginBuffer(int shader, BlendMode blend, Color tint, float alpha) { }

    /// <summary>Ends the buffered group and composites it into its layer.</summary>
    void EndBuffer() { }

    /// <summary>Routes the draws that follow back to the canvas itself, over every layer.</summary>
    void EndLayers() { }

    /// <summary>
    /// A rect on the target in the canvas's own coordinates, for something that
    /// must cover the screen whatever the canvas is shifted by. Canvases that
    /// don't move draw on the target as it is.
    /// </summary>
    Rect2 ToLocal(Rect2 screen) => screen;

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
            float angle = Mathf.Tau * i / segments;
            points[i] = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius);
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
        TextGlow glow,
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
        ShapeGlow glow)
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
