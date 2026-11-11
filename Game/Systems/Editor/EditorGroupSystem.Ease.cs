using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Ui;
using Xanadu.Timeline;
using Xanadu.Slang;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group panel's side of the shared ease picker: an EASE button opens it
/// for the selected effect or key, and each frame this refreshes the curve it
/// shows and applies a pick as one undo step. The picker stays open while the
/// selection changes, like the animator's.
/// </summary>
public sealed partial class EditorGroupSystem
{
    /// <summary>Opens the picker from an EASE button, or closes it when it is already open from that button.</summary>
    private static void ToggleEasePicker(ref EditorGroupState state, EditorEaseTarget target, Entity button)
    {
        Rect2 anchor = XifEditorSystem.Area(button);
        bool again = state.EaseFor == target && state.EaseAnchor == anchor;
        state.EaseFor = again ? EditorEaseTarget.None : target;
        state.EaseAnchor = anchor;
    }

    private void UpdateEasePicker(Entity editor, ref EditorGroupState state, GroupDefinition group)
    {
        EasePreset? pick = state.EasePick;
        state.EasePick = null;
        state.EaseCurrent = null;

        if (state.EaseFor == EditorEaseTarget.Effect)
        {
            int row = state.SelectedEffect;
            if (state.SelectedGroup != state.Open || row < 0 || row >= group.Effects.Count)
                return;

            if (pick is { } ease && group.Effects[row].Ease != ease)
                Edit(() => group.Effects[row] = group.Effects[row] with { Ease = ease });
            state.EaseCurrent = group.Effects[row].Ease;
        }
        else if (state.EaseFor == EditorEaseTarget.Key)
        {
            if (!TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys) ||
                !HasSelectedKey(state, keys, uniform, out ShaderKey key))
            {
                return;
            }

            float time = state.SelectedKey;
            if (pick is { } ease && key.Ease != ease)
            {
                Edit(() => EditorShaderKeyEdits.SetEase(keys, uniform.Name, time, ease));
                HasSelectedKey(state, keys, uniform, out key);
            }

            state.EaseCurrent = key.Ease;
        }
    }

    /// <summary>Where the picker is on screen while it is open, else an empty box.</summary>
    private static Rect2 EasePickerArea(Entity editor, EditorGroupState state)
    {
        if (!EditorGroupEaseHost.IsOpen(state))
            return new Rect2();

        var bounds = new Rect2(Vector2.Zero, editor.GetComponent<PlayfieldView>().ScreenSize);
        Vector2 size = EasePickerPanel.Size;
        return new Rect2(EasePickerSystem.Corner(state.EaseAnchor, bounds, size), size);
    }
}
