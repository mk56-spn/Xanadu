using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Logic.Animator;

/// <summary>
/// Keyboard shortcuts. The keys every timeline shares come from
/// <see cref="TimelineShortcuts"/>; the actions buttons perform come from
/// <see cref="AnimatorBindings"/> and run through the session's intent listeners,
/// exactly as a pressed button does. Left and Right alone also step, as the
/// animator has no selection for them to nudge.
/// </summary>
public sealed class AnimatorShortcutSystem(IKeyPressSource keys)
    : QuerySystem<AnimatorDocument>
{
    protected override void OnUpdate()
    {
        // Typing in a name field or having the NEW CLIP dialog open must not
        // trigger shortcuts such as Delete or Space, now or once it closes.
        bool blocked = IsBlocked();
        while (keys.TryTake(out KeyChord chord))
        {
            if (!blocked)
                Press(chord);
        }
    }

    private bool IsBlocked()
    {
        if (Query.Store.Query<TextField>().AllTags(Tags.Get<TextFieldFocused>()).Count > 0)
            return true;

        foreach (Entity session in Query.Store.Query<AnimatorUi>().Entities)
        {
            if (session.GetComponent<AnimatorUi>().NewClipDialog.IsOpen())
                return true;
        }

        return false;
    }

    private void Press(KeyChord chord)
    {
        TimelineCommand? command = TimelineShortcuts.Find(chord) ?? chord switch
        {
            { Key: Key.Left, Ctrl: false, Shift: false } => TimelineCommand.StepBack,
            { Key: Key.Right, Ctrl: false, Shift: false } => TimelineCommand.StepForward,
            _ => null,
        };
        AnimatorBindings.Binding? binding = AnimatorBindings.Find(chord.Key, chord.Ctrl, chord.Shift);

        foreach (Entity session in Query.ToEntityList())
        {
            if (command is { } timeline)
                TimelineShortcuts.Run(session, timeline);
            else if (binding is { } bound)
                bound.Intent.EmitOn(session);
        }
    }
}
