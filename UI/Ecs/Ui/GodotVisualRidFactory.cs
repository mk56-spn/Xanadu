using Godot;
using Xanadu.Systems.Canvas;

namespace Xanadu.Ecs;

/// <summary>Creates real RenderingServer canvas items.</summary>
public sealed class GodotVisualRidFactory : IVisualRidFactory
{
    public bool LoadsAssets => true;

    public Rid CreateCanvasItem() => CanvasServer.Current.CreateItem();
}
