using Godot;

namespace Writer.Systems.Canvas;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    void SetVisible(Rid canvas, bool visible);

    void SetTransform(Rid canvas, Transform2D transform);

    void Clear(Rid canvas);

    void DrawRect(Rid canvas, Rect2 bounds, Color color);

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
