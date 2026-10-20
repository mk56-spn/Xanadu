using Godot;

namespace Writer.Ui;

/// <summary>Creates canvas items for UI entities.</summary>
public interface IVisualRidFactory
{
    Rid CreateCanvasItem();
}
