using Godot;
using Xanadu.Systems.Canvas;

namespace Xanadu.Ecs;

/// <summary>Creates canvas items on whichever <see cref="CanvasServer"/> is installed, without loading Godot assets.</summary>
public sealed class CanvasServerRidFactory : IVisualRidFactory
{
    public Rid CreateCanvasItem() => CanvasServer.Current.CreateItem();
}
