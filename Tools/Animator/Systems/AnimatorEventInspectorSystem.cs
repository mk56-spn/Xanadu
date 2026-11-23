using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the event inspector above the timeline while a clip event is
/// selected and the preview is being edited. The name and time apply when
/// their field loses focus, as in the other inspectors.
/// </summary>
public sealed class AnimatorEventInspectorSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _nameWasFocused;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        if (ui.EventInspector.IsNull || ui.Timeline.IsNull)
            return;

        int index = AnimatorEventCommands.Selected(session, out RigClip? clip);
        RigEvent? selected = index >= 0 && clip is not null ? clip.Events[index] : null;
        bool shown = selected is not null && !AnimatorPreviewModeCommands.IsOn(session) &&
                     !session.GetComponent<AnimatorModes>().Rest;
        UiLayout.SetHidden(ui.EventInspector, !shown);

        UiText.Set(ui.EventTitle, selected is null ? "EVENT" : $"EVENT IN {clip!.Id.ToUpperInvariant()}");
        _nameWasFocused = Sync(ui.EventName, _nameWasFocused, selected?.Name ?? "",
            typed => AnimatorEventCommands.Rename(session, typed));

    }

    /// <summary>Applies a field's text when it loses focus and shows the current value while it has none.</summary>
    private static bool Sync(Entity field, bool wasFocused, string value, System.Action<string> apply)
    {
        if (field.IsNull || !field.HasComponent<TextField>())
            return false;

        bool focused = field.Tags.Has<TextFieldFocused>();
        ref TextField text = ref field.GetComponent<TextField>();
        if (!focused && wasFocused)
            apply(text.Value);
        if (!focused)
            text.Value = value;
        return focused;
    }
}
