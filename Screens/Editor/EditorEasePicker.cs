using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Levels;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>
/// Owns the shared ease picker for the group keys at the playhead: open while
/// EASE is lit, above the EASE button, showing the curve the keys there leave
/// with; a picked curve eases those keys.
/// </summary>
public static class EditorEasePicker
{
    /// <summary>Builds the picker on <paramref name="layer"/> and has it answer to the editor in <paramref name="store"/>.</summary>
    public static Entity Build(UiNode layer, EntityStore store)
    {
        Entity picker = EasePickerPanel.Build(layer);
        picker.AddSignalHandler<EasePickerFrameDue>(signal =>
            Describe(signal.Entity, EditorGroupPanelSystem.Editor(store), store));
        picker.AddSignalHandler<EasePicked>(signal =>
            EditorGroupPanelSystem.Editor(store).EmitOnEditor(new EaseGroupKeys(signal.Event.Preset)));
        picker.AddSignalHandler<EasePickerCloseAsked>(_ =>
            EditorGroupPanelSystem.Editor(store).EmitOnEditor(new ShowGroupEase(false)));
        return picker;
    }

    private static void Describe(Entity picker, Entity editor, EntityStore store)
    {
        ref EasePickerFrame frame = ref picker.GetComponent<EasePickerFrame>();
        frame.EmptyHint = "Move the playhead onto a key";
        frame.Open = !editor.IsNull && editor.GetComponent<EditorGroupEdit>().Easing &&
                     EditorGroupTarget.Picked(editor) is not null;
        frame.Anchor = Anchor(store);
        frame.Bounds = editor.IsNull ? new Rect2() : new Rect2(Vector2.Zero, editor.GetComponent<PlayfieldView>().ScreenSize);
        frame.Current = null;
        if (editor.IsNull)
            return;

        (GroupDocument? group, string? node) = EditorGroupTarget.Of(editor);
        if (group is not null && node is not null)
            frame.Current = group.Clip.ReadEase(group.ClipTime(EditorGroupCommands.PlayheadBeat(editor)), node);
    }

    private static Rect2 Anchor(EntityStore store)
    {
        Entity button = UiCommand.Find(store, EditorButtonKind.GroupEase);
        if (button.IsNull || !button.HasComponent<Rectangle>() || !button.HasComponent<Position>())
            return new Rect2();

        Vector2 centre = Xanadu.Ui.Ecs.UiTransform.WorldPosition(button, button.GetComponent<Position>().Value);
        Vector2 size = button.GetComponent<Rectangle>().Bounds.Size;
        return new Rect2(centre - size / 2, size);
    }
}
