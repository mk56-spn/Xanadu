using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>
/// Keyboard shortcuts that choose the editor placement mode: the number keys,
/// on the main row or the keypad, in tool order.
/// </summary>
public static class EditorPlacementKeybinds
{
    private static readonly (Key Main, Key Pad, EditorPlacementMode Mode)[] Bindings =
    {
        (Key.Key1, Key.Kp1, EditorPlacementMode.Notes),
        (Key.Key2, Key.Kp2, EditorPlacementMode.Neutral),
        (Key.Key3, Key.Kp3, EditorPlacementMode.Dash),
        (Key.Key4, Key.Kp4, EditorPlacementMode.Momentum),
        (Key.Key5, Key.Kp5, EditorPlacementMode.Blocks),
        (Key.Key6, Key.Kp6, EditorPlacementMode.Spikes),
        (Key.Key7, Key.Kp7, EditorPlacementMode.Contact),
        (Key.Key8, Key.Kp8, EditorPlacementMode.Mines),
        (Key.Key9, Key.Kp9, EditorPlacementMode.FrictionBar),
        (Key.Key0, Key.Kp0, EditorPlacementMode.Text),
    };

    public static bool TryGet(Key key, out EditorPlacementMode mode)
    {
        foreach ((Key main, Key pad, EditorPlacementMode bound) in Bindings)
        {
            if (key == main || key == pad)
            {
                mode = bound;
                return true;
            }
        }

        mode = EditorPlacementMode.Notes;
        return false;
    }

    /// <summary>Label shown in the F9 editor-binding list.</summary>
    public static string LabelFor(EditorPlacementMode mode)
    {
        foreach ((Key main, _, EditorPlacementMode bound) in Bindings)
        {
            if (bound == mode)
                return main == Key.Key0 ? "0" : ((int)main - (int)Key.Key0).ToString();
        }

        return "";
    }
}
