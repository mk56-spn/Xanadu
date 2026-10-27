using Godot;

namespace Xanadu.Systems.Input;

/// <summary>PageUp/PageDown debug overlay paging.</summary>
public sealed class GodotOverlayPageSource : IOverlayPageSource
{
    public bool PreviousPressed() => Godot.Input.IsKeyPressed(Key.Pageup);

    public bool NextPressed() => Godot.Input.IsKeyPressed(Key.Pagedown);
}
