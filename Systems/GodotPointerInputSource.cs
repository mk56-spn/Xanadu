using Godot;

namespace Writer.Systems;

/// <summary>Reads pointer state from the Godot Control that hosts the UI.</summary>
public sealed class GodotPointerInputSource(Control host) : IPointerInputSource
{
    public PointerInput Read() =>
        new(host.GetLocalMousePosition(), Input.IsMouseButtonPressed(MouseButton.Left));
}
