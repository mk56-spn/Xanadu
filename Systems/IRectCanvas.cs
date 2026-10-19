using Godot;

namespace Writer.Systems;

/// <summary>Canvas operations used by draw systems.</summary>
public interface IRectCanvas
{
    void Clear(Rid canvas);

    void DrawRect(Rid canvas, Rect2 bounds, Color color);
}
