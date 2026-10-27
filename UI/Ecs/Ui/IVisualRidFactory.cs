using Godot;

namespace Xanadu.Ecs;

/// <summary>Creates canvas items for UI entities.</summary>
public interface IVisualRidFactory
{
    Rid CreateCanvasItem();
}
