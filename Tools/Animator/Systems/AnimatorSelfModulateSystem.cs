using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Sets the selected attachment's self modulate from the POSE tab's colour
/// field and picker, as one undo step. A colour goes to what was selected when typing
/// or dragging began, since clicking another attachment is what ends typing.
/// </summary>
public sealed class AnimatorSelfModulateSystem : QuerySystem<AnimatorDocument, AnimatorUi, AnimatorSelection>
{
    private int _target = -1;
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        Entity field = session.GetComponent<AnimatorUi>().ModulateField;
        if (field.IsNull || !field.HasComponent<UiColorField>())
            return;

        var attachments = session.GetComponent<AnimatorDocument>().Package.Attachments;
        int selected = session.GetComponent<AnimatorSelection>().Attachment;
        bool editing = UiColorField.IsEditing(field);
        if (editing && !_wasEditing)
            _target = selected;
        _wasEditing = editing;

        if (UiColorField.TakeCommit(field, out Color color))
        {
            int target = _target;
            if (target >= 0 && target < attachments.Count)
            {
                AnimatorHistoryCommands.Edit(session, () =>
                    session.GetComponent<AnimatorDocument>().Package.Attachments[target].SelfModulate = color);
                AnimatorSession.SetStatus(session, "COLOUR SET");
            }
            else
            {
                AnimatorSession.SetStatus(session, "SELECT AN ATTACHMENT FIRST");
            }
        }

        UiColorField.Show(field, selected >= 0 && selected < attachments.Count
            ? attachments[selected].SelfModulate
            : Colors.White);
    }
}
