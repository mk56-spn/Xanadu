using System;
using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// A canvas item a system group creates for its own drawing: parented, ordered,
/// and released with the group. Groups own these so the system root only wires
/// phases together instead of holding a free-floating RID per layer.
/// </summary>
public sealed class CanvasItemHandle : IDisposable
{
    private bool _released;

    /// <param name="parent">The canvas item this layer is parented to.</param>
    /// <param name="zIndex">Draw order among siblings.</param>
    /// <param name="visible">Whether the layer starts visible.</param>
    public CanvasItemHandle(Rid parent, int zIndex, bool visible = true)
    {
        Rid = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(Rid, parent);
        CanvasServer.Current.SetZIndex(Rid, zIndex);
        if (!visible)
            CanvasServer.Current.SetVisible(Rid, false);
    }

    public Rid Rid { get; }

    public void SetVisible(bool visible)
    {
        if (!_released)
            CanvasServer.Current.SetVisible(Rid, visible);
    }

    public void Dispose()
    {
        if (_released)
            return;

        _released = true;
        CanvasServer.Current.Free(Rid);
    }
}
