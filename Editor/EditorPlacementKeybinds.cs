using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>
/// Keyboard shortcuts that choose the editor placement mode. Number keys keep
/// the mapping extensible as more note kinds get shortcuts.
/// </summary>
public static class EditorPlacementKeybinds
{
    public static readonly Key Normal = Key.Key1;
    public static readonly Key Pair = Key.Key2;
    public static readonly Key Dash = Key.Key3;
    public static readonly Key Momentum = Key.Key4;
    public static readonly Key Block = Key.Key5;
    public static readonly Key Spike = Key.Key6;
    public static readonly Key WeaponSwitch = Key.Key7;
    public static readonly Key Channel = Key.Key8;
    public static readonly Key FormStep = Key.Key9;

    public static bool TryGet(Key key, out EditorPlacementMode mode)
    {
        if (key == Normal || key == Key.Kp1)
        {
            mode = EditorPlacementMode.Notes;
            return true;
        }

        if (key == Pair || key == Key.Kp2)
        {
            mode = EditorPlacementMode.Pair;
            return true;
        }

        if (key == Dash || key == Key.Kp3)
        {
            mode = EditorPlacementMode.Dash;
            return true;
        }

        if (key == Momentum || key == Key.Kp4)
        {
            mode = EditorPlacementMode.Momentum;
            return true;
        }

        if (key == Block || key == Key.Kp5)
        {
            mode = EditorPlacementMode.Blocks;
            return true;
        }

        if (key == Spike || key == Key.Kp6)
        {
            mode = EditorPlacementMode.Spikes;
            return true;
        }

        if (key == WeaponSwitch || key == Key.Kp7)
        {
            mode = EditorPlacementMode.WeaponSwitch;
            return true;
        }

        if (key == Channel || key == Key.Kp8)
        {
            mode = EditorPlacementMode.Channel;
            return true;
        }

        if (key == FormStep || key == Key.Kp9)
        {
            mode = EditorPlacementMode.FormStep;
            return true;
        }

        mode = EditorPlacementMode.Notes;
        return false;
    }

    /// <summary>Label shown in the F2 editor-binding list.</summary>
    public static string LabelFor(EditorPlacementMode mode) => mode switch
    {
        EditorPlacementMode.Notes => "1",
        EditorPlacementMode.Pair => "2",
        EditorPlacementMode.Dash => "3",
        EditorPlacementMode.Momentum => "4",
        EditorPlacementMode.Blocks => "5",
        EditorPlacementMode.Spikes => "6",
        EditorPlacementMode.WeaponSwitch => "7",
        EditorPlacementMode.Channel => "8",
        EditorPlacementMode.FormStep => "9",
        _ => "",
    };
}
