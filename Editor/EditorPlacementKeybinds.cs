using Godot;
using Writer.Ecs;

namespace Writer.Editor;

/// <summary>
/// Keyboard shortcuts that choose the editor placement mode. Number keys keep
/// the mapping extensible as more note kinds get shortcuts.
/// </summary>
public static class EditorPlacementKeybinds
{
    public static readonly Key Normal = Key.Key1;
    public static readonly Key Dash = Key.Key2;

    public static bool TryGet(Key key, out EditorPlacementMode mode)
    {
        if (key == Normal || key == Key.Kp1)
        {
            mode = EditorPlacementMode.Notes;
            return true;
        }

        if (key == Dash || key == Key.Kp2)
        {
            mode = EditorPlacementMode.Movement;
            return true;
        }

        mode = EditorPlacementMode.Notes;
        return false;
    }

    /// <summary>Label shown in the F2 editor-binding list.</summary>
    public static string LabelFor(EditorPlacementMode mode) => mode switch
    {
        EditorPlacementMode.Notes => "1",
        EditorPlacementMode.Movement => "2",
        _ => "",
    };
}
