using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Owns the shared ease picker in the animator: it opens from the timeline's
/// EASING button, shows the selected key set's curve and sets a picked one on
/// it through the Interpolate action, so undo and the status line stay the
/// same as the 1 to 5 keys.
/// </summary>
public static class AnimatorEasePicker
{
    /// <summary>Builds the picker on <paramref name="layer"/> and has it answer to the animator in <paramref name="store"/>.</summary>
    public static Entity Build(UiNode layer, EntityStore store)
    {
        Entity picker = EasePickerPanel.Build(layer, Shortcut);
        picker.AddSignalHandler<EasePickerFrameDue>(signal => Describe(signal.Entity, AnimatorSession.Find(store)));
        picker.AddSignalHandler<EasePicked>(signal =>
        {
            Entity session = AnimatorSession.Find(store);
            if (!session.IsNull)
                session.EmitSignal(new SetEase(signal.Event.Preset));
        });
        picker.AddSignalHandler<EasePickerCloseAsked>(_ =>
        {
            Entity session = AnimatorSession.Find(store);
            if (!session.IsNull)
                AnimatorUi.Of(session).EasePicker.SetOpen(false);
        });
        return picker;
    }

    private static void Describe(Entity picker, Entity session)
    {
        ref EasePickerFrame frame = ref picker.GetComponent<EasePickerFrame>();
        frame.EmptyHint = "Select keys to set their curve";
        frame.Open = !session.IsNull && AnimatorUi.Of(session).EasePicker.IsOpen() && !AnimatorPreviewMode.IsOn(session);
        if (session.IsNull)
            return;

        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        frame.Anchor = AnimatorGeometry.PanelArea(ui.EasingButton);
        frame.Bounds = new Rect2(Vector2.Zero, ui.ScreenSize);
        frame.Current = CurrentPreset(session);
    }

    /// <summary>The selected key set's curve, or null when no key is selected.</summary>
    public static EasePreset? CurrentPreset(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        RigClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null || !selection.HasKey)
            return null;

        return selection.KeyProperty is { } property
            ? RigEasing.Read(clip, selection.KeyTarget, selection.KeyId, property, selection.KeyTime)
            : RigEasing.Read(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime);
    }

    /// <summary>The key that sets the curve, for the five that have one.</summary>
    public static string Shortcut(EasePreset preset) => preset switch
    {
        EasePreset.Step => " (1)",
        EasePreset.Linear => " (2)",
        EasePreset.Ease => " (3)",
        EasePreset.EaseIn => " (4)",
        EasePreset.EaseOut => " (5)",
        _ => "",
    };
}
