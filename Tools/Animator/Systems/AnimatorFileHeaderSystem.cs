using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The file controls in the window header: the path field shows and edits where
/// Save and Load go, and a label flags unsaved changes.
/// </summary>
public sealed class AnimatorFileHeaderSystem : QuerySystem<AnimatorFile, AnimatorUi>
{
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            SyncPath(session, ui.PathField);
            SyncDirty(session, ui.DirtyLabel);
        }
    }

    private void SyncPath(Entity session, Entity field)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return;

        bool editing = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        if (!editing && _wasEditing)
            AnimatorFileCommands.SetPath(session, text.Value);

        if (!editing)
            text.Value = session.GetComponent<AnimatorFile>().Path;

        _wasEditing = editing;
    }

    private static void SyncDirty(Entity session, Entity label)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        string value = session.GetComponent<AnimatorFile>().Dirty ? "● UNSAVED" : "";
        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        label.AddTag<Dirty>();
    }
}
