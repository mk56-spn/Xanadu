using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>Creates canvas items on whichever <see cref="CanvasServer"/> is installed, without loading Godot assets.</summary>
public sealed class CanvasServerRidFactory(bool loadsAssets = false) : IVisualRidFactory
{
    public bool LoadsAssets => loadsAssets;

    public Handle CreateCanvasItem() => CanvasServer.Current.CreateItem();
}
