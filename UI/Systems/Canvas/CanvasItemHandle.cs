using System;

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
    public CanvasItemHandle(Handle parent, int zIndex, bool visible = true)
    {
        Handle = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(Handle, parent);
        CanvasServer.Current.SetZIndex(Handle, zIndex);
        if (!visible)
            CanvasServer.Current.SetVisible(Handle, false);
    }

    public Handle Handle { get; }

    public void SetVisible(bool visible)
    {
        if (!_released)
            CanvasServer.Current.SetVisible(Handle, visible);
    }

    public void Dispose()
    {
        if (_released)
            return;

        _released = true;
        CanvasServer.Current.Free(Handle);
    }
}
