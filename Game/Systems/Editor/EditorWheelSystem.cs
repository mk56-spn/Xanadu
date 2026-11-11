using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The wheel the UI left over: seeks the song, faster with Shift, and Ctrl zooms
/// the field view about the pointer. A warning, the group overlay or a text field
/// in use keeps the wheel from reaching the editor.
/// </summary>
public sealed class EditorWheelSystem(EditorCommandBus commands, IWheelSource wheel)
    : QuerySystem<Playhead, PlayfieldView>
{
    protected override void OnUpdate()
    {
        int notches = wheel.TakeWheel();
        if (notches == 0 || IsWheelBlocked())
            return;

        foreach (Entity editor in Query.ToEntityList())
        {
            if (wheel.Control && editor.HasComponent<EditorViewNavigation>())
            {
                editor.GetComponent<EditorViewNavigation>().ZoomSteps += notches;
                return;
            }

            // Wheel up goes back through the song.
            commands.Enqueue(EditorCommand.Seek(
                EditorScroll.Target(editor.GetComponent<Playhead>(), -notches, wheel.Shift)));
            return;
        }
    }

    private bool IsWheelBlocked()
    {
        EntityStore store = Query.Store;
        if (store.Query<WarningPopup>().Count > 0 ||
            store.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).Count > 0)
        {
            return true;
        }

        foreach (Entity entity in store.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().Open >= 0)
                return true;
        }

        return false;
    }
}
