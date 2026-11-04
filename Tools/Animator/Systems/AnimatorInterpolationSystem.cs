using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Lights the EASING window's curve that the selected key set uses.</summary>
public sealed class AnimatorInterpolationSystem : QuerySystem<ToolAction, ButtonVisual>
{
    private static readonly Color Active = Palette.Selected;

    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        XafEasePreset? current = CurrentPreset(session);
        // Hidden cells too, so the window opens already lit.
        foreach (Entity button in Query.Store.Query<ToolAction, ButtonVisual>().WithDisabled().ToEntityList())
        {
            ToolAction action = button.GetComponent<ToolAction>();
            if (action.Kind != ToolActionKind.Interpolate)
                continue;

            bool on = current is { } preset && action.Argument == preset.ToString();
            Color target = on ? Active : AnimatorLayout.EasingCell;
            ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }

    /// <summary>The selected key set's curve, or null when no key is selected.</summary>
    public static XafEasePreset? CurrentPreset(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null || !selection.HasKey)
            return null;

        return selection.KeyProperty is { } property
            ? XafEasing.Read(
                clip, selection.KeyTarget, selection.KeyId, property, selection.KeyTime)
            : XafEasing.Read(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime);
    }
}
