using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Sets the selected attachment's self modulate from the POSE tab's colour
/// field and picker, as one undo step. A colour goes to what was selected when typing
/// or dragging began, since clicking another attachment is what ends typing.
/// A dragged picker colours the attachment as it moves, inside one gesture.
/// </summary>
public sealed class AnimatorSelfModulateSystem : QuerySystem<AnimatorDocument, AnimatorUi, AnimatorSelection>
{
    private int _target = -1;
    private bool _wasEditing;
    private bool _gesture;

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

        if (UiColorField.TakeLive(field, out Color live) && _target >= 0 && _target < attachments.Count)
        {
            if (!_gesture)
            {
                AnimatorHistoryCommands.BeginGesture(session);
                _gesture = true;
            }

            attachments[_target].SelfModulate = live;
        }

        if (UiColorField.TakeCommit(field, out Color color))
        {
            int target = _target;
            if (target >= 0 && target < attachments.Count)
            {
                void Set() => session.GetComponent<AnimatorDocument>().Package.Attachments[target].SelfModulate = color;
                if (_gesture)
                    AnimatorHistoryCommands.EndGesture(session, Set);
                else
                    AnimatorHistoryCommands.Edit(session, Set);
                _gesture = false;
                AnimatorSession.SetStatus(session, "COLOUR SET");
            }
            else
            {
                AnimatorSession.SetStatus(session, "SELECT AN ATTACHMENT FIRST");
            }
        }

        // A drag that ends where it began commits nothing, and its gesture closes empty.
        if (_gesture && !editing)
        {
            AnimatorHistoryCommands.EndGesture(session);
            _gesture = false;
        }

        UiColorField.Show(field, selected >= 0 && selected < attachments.Count
            ? attachments[selected].SelfModulate
            : Colors.White);
    }
}
