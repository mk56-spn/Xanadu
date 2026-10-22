using Godot;

namespace Writer.Systems.Canvas;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    void SetVisible(Rid canvas, bool visible);

    void SetTransform(Rid canvas, Transform2D transform);

    void Clear(Rid canvas);

    void DrawRect(Rid canvas, Rect2 bounds, Color color);

    /// <summary>Draws a filled circle.</summary>
    void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) { }

    /// <summary>Draws an outlined circle using short line segments.</summary>
    void DrawCircleOutline(
        Rid canvas,
        Vector2 center,
        float radius,
        Color color,
        float width,
        int segments = 40)
    {
        Vector2 previous = center + new Vector2(radius, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            Vector2 next = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius);
            DrawLine(canvas, previous, next, color, width);
            previous = next;
        }
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
