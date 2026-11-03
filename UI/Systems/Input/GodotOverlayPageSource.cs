using Godot;

namespace Xanadu.Systems.Input;

/// <summary>PageUp/PageDown paging, Up/Down selection, Home sort cycling.</summary>
public sealed class GodotOverlayPageSource : IOverlayPageSource
{
    public bool PreviousPressed() => Godot.Input.IsKeyPressed(Key.Pageup);

    public bool NextPressed() => Godot.Input.IsKeyPressed(Key.Pagedown);

    public bool SelectionUpPressed() => Godot.Input.IsKeyPressed(Key.Up);

    public bool SelectionDownPressed() => Godot.Input.IsKeyPressed(Key.Down);

    public bool SortTogglePressed() => Godot.Input.IsKeyPressed(Key.Home);
}
