using System;
using Godot;

namespace Writer.Systems.Canvas;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    void SetVisible(Rid canvas, bool visible);

    void SetTransform(Rid canvas, Transform2D transform);

    void Clear(Rid canvas);

    void DrawRect(Rid canvas, Rect2 bounds, Color color);

    /// <summary>Draws a texture into a rectangle.</summary>
    void DrawTexture(Rid canvas, Rect2 bounds, Texture2D texture) { }

    /// <summary>Draws a filled circle.</summary>
    void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) { }

    /// <summary>Fills a convex polygon.</summary>
    void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) { }

    /// <summary>Draws a connected polyline.</summary>
    void DrawPolyline(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true)
    {
        for (int i = 1; i < points.Length; i++)
            DrawLine(canvas, points[i - 1], points[i], color, width);
    }

    /// <summary>Draws an outlined circle as one connected polyline.</summary>
    void DrawCircleOutline(
        Rid canvas,
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

    void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width);

    void DrawText(Rid canvas, Rect2 bounds, string text, Color color);

    /// <summary>Draws centered text with an explicit font size.</summary>
    void DrawTextStyled(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawText(canvas, bounds, text, color);

    void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color);

    void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color);

    /// <summary>Draws vertically oriented text.</summary>
    void DrawTextVertical(
        Rid canvas,
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
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextStyled(canvas, bounds, text, color, fontSize);

    /// <summary>Draws right-aligned text with an explicit font size.</summary>
    void DrawTextStyledRight(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextStyled(canvas, bounds, text, color, fontSize);
}
