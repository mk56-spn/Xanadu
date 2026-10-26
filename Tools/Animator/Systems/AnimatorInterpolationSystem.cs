using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;

namespace Writer.Tools.Animator.Systems;

/// <summary>Highlights the interpolation preset buttons that match the selected key set.</summary>
public sealed class AnimatorInterpolationSystem : QuerySystem<ToolAction, ButtonVisual>
{
    private static readonly Color Active = new("#0f766e");

    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        XafEasePreset? current = CurrentPreset(session);
        foreach (Entity button in Query.ToEntityList())
        {
            ToolAction action = button.GetComponent<ToolAction>();
            if (action.Kind != ToolActionKind.Interpolate)
                continue;

            bool on = current is { } preset && action.Argument == preset.ToString();
            Color target = on ? Active : UiTheme.Button;
            ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
            if (visual.Normal == target)
                continue;

            visual.Normal = target;
            visual.Hover = target.Lerp(Colors.White, 0.16f);
        }
    }

    private static XafEasePreset? CurrentPreset(Entity session)
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
