using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the event inspector above the timeline while a clip event is
/// selected and the preview is being edited. The name and time apply when
/// their field loses focus, as in the other inspectors.
/// </summary>
public sealed class AnimatorEventInspectorSystem : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _nameWasFocused;
    private bool _timeWasFocused;

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

        int index = AnimatorEventCommands.Selected(session, out XafClip? clip);
        XafEvent? selected = index >= 0 && clip is not null ? clip.Events[index] : null;
        bool shown = selected is not null && !AnimatorPreviewModeCommands.IsOn(session);
        Rect2 timeline = AnimatorGeometry.PanelArea(ui.Timeline);
        Vector2 topLeft = timeline.Position +
                          new Vector2(AnimatorTimelineModel.LabelWidth + 24f, -AnimatorLayout.EventInspectorSize.Y - 8f);
        AnimatorMenuPlacement.Place(ui.EventInspector, topLeft + AnimatorLayout.EventInspectorSize / 2, shown: shown);

        UiText.Set(ui.EventTitle, selected is null ? "EVENT" : $"EVENT IN {clip!.Id.ToUpperInvariant()}");
        _nameWasFocused = Sync(ui.EventName, _nameWasFocused, selected?.Name ?? "",
            typed => AnimatorEventCommands.Rename(session, typed));
        _timeWasFocused = Sync(ui.EventTime, _timeWasFocused,
            selected is null ? "" : selected.Time.ToString("0.###", CultureInfo.InvariantCulture),
            typed =>
            {
                if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
                    AnimatorEventCommands.Retime(session, time);
                else
                    AnimatorSession.SetStatus(session, "NOT A NUMBER");
            });
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
