using Xanadu.Systems;

namespace Xanadu.Systems.Input;

/// <summary>PageUp/PageDown paging, Up/Down selection, Home sort cycling, End dumping.</summary>
public sealed class KeyOverlayPageSource : IOverlayPageSource
{
    private static bool Held(Key key) => HostPlatform.Keyboard.IsPressed(key);

    public bool PreviousPressed() => Held(Key.Pageup);

    public bool NextPressed() => Held(Key.Pagedown);

    public bool SelectionUpPressed() => Held(Key.Up);

    public bool SelectionDownPressed() => Held(Key.Down);

    public bool SortTogglePressed() => Held(Key.Home);

    public bool DumpPressed() => Held(Key.End);
}
