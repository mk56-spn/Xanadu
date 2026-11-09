using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Hosts the shared ease picker in the animator: it opens from the timeline's
/// EASING button, shows the selected key set's curve and sets a picked one on
/// it through the Interpolate action, so undo and the status line stay the
/// same as the 1 to 5 keys.
/// </summary>
public sealed class AnimatorEasePickerHost(EntityStore store) : IEasePickerHost
{
    private Entity Session => AnimatorSession.Find(store);

    public bool Open
    {
        get
        {
            Entity session = Session;
            return !session.IsNull &&
                   session.GetComponent<AnimatorEasingWindow>().Open &&
                   !AnimatorPreviewModeCommands.IsOn(session);
        }
    }

    public Rect2 Anchor => Session is { IsNull: false } session
        ? AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().EasingButton)
        : new Rect2();

    public Rect2 Bounds => Session is { IsNull: false } session
        ? new Rect2(Vector2.Zero, session.GetComponent<AnimatorUi>().ScreenSize)
        : new Rect2();

    public EasePreset? Current => Session is { IsNull: false } session ? CurrentPreset(session) : null;

    public string EmptyHint => "Select keys to set their curve";

    public void Pick(EasePreset preset)
    {
        Entity session = Session;
        if (!session.IsNull)
            AnimatorCommands.Run(session, new ToolAction(ToolActionKind.Interpolate, preset.ToString()));
    }

    public void Close()
    {
        Entity session = Session;
        if (!session.IsNull)
            session.GetComponent<AnimatorEasingWindow>().Open = false;
    }

    public string TooltipNote(EasePreset preset) => Shortcut(preset);

    /// <summary>The selected key set's curve, or null when no key is selected.</summary>
    public static EasePreset? CurrentPreset(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null || !selection.HasKey)
            return null;

        return selection.KeyProperty is { } property
            ? XafEasing.Read(clip, selection.KeyTarget, selection.KeyId, property, selection.KeyTime)
            : XafEasing.Read(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime);
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
