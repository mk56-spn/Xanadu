using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor;

/// <summary>
///     Keyboard shortcuts that choose the editor placement mode: the number keys,
///     on the main row or the keypad, in tool order. The note tool is 1.
/// </summary>
public static class EditorPlacementKeybinds
{
    private static readonly (Key Main, Key Pad, EditorPlacementMode Mode)[] Bindings =
    {
        (Key.Key1, Key.Kp1, EditorPlacementMode.Note),
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

        mode = EditorPlacementMode.Note;
        return false;
    }

    /// <summary>Label shown in the F9 editor-binding list and on the tool's button.</summary>
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
