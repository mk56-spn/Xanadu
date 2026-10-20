using Godot;

namespace Writer.Systems.Canvas;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    void SetVisible(Rid canvas, bool visible);

    void SetTransform(Rid canvas, Transform2D transform);

    void Clear(Rid canvas);

    void DrawRect(Rid canvas, Rect2 bounds, Color color);

    void DrawText(Rid canvas, Rect2 bounds, string text, Color color);

    void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color);

    void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color);
}
