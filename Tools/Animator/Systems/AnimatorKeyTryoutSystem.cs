using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Runs the quick test after keying: steps the playhead from the previous key
/// to the new one in real time, holds there a moment and loops; shows the ease bar while
/// the test is on, lighting the ease in use; and ends the test when anything
/// else plays, moves the playhead, switches clip or changes the selection.
/// </summary>
public sealed class AnimatorKeyTryoutSystem : QuerySystem<AnimatorKeyTryout, AnimatorUi>
{
    /// <summary>Seconds the loop rests on each key before going round again.</summary>
    private const float LoopHold = 0.35f;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity session in Query.ToEntityList())
            Update(session, delta);
    }

    private static void Update(Entity session, float delta)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        ref AnimatorKeyTryout tryout = ref session.GetComponent<AnimatorKeyTryout>();
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorRuntime runtime = document.Runtime;

        if (tryout.Active && Interrupted(session, tryout, runtime))
            tryout.Active = tryout.Playing = false;

        if (tryout.Active && tryout.Playing)
        {
            tryout.Elapsed += delta;
            float span = tryout.To - tryout.From;
            float cycle = LoopHold + span + LoopHold;
            if (tryout.Elapsed >= cycle)
                tryout.Elapsed %= cycle;
            float time = Math.Clamp(tryout.From + tryout.Elapsed - LoopHold, tryout.From, tryout.To);
            if (Math.Abs(runtime.CurrentTime - time) > 0.00001f)
                runtime.Seek(time);
            tryout.Shown = runtime.CurrentTime;
        }

        if (ui.TryoutBar.IsNull)
            return;
        if (UiLayout.IsHidden(ui.TryoutBar) == tryout.Active)
            UiLayout.SetHidden(ui.TryoutBar, !tryout.Active);
        if (tryout.Active)
            Light(ui, tryout.Picked);
    }

    private static bool Interrupted(Entity session, AnimatorKeyTryout tryout, AnimatorRuntime runtime)
    {
        if (!runtime.Paused || runtime.CurrentClip != tryout.Clip)
            return true;
        if (Math.Abs(runtime.CurrentTime - tryout.Shown) > 0.0005f)
            return true;
        if (session.GetComponent<AnimatorModes>().Rest)
            return true;

        var now = AnimatorKeyTryoutCommands.Targets(session);
        if (now.Count != tryout.Targets.Count)
            return true;
        foreach (var target in now)
        {
            if (!tryout.Targets.Contains(target))
                return true;
        }
        return false;
    }

    private static void Light(AnimatorUi ui, EasePreset? picked)
    {
        if (ui.TryoutEases is null)
            return;
        for (int i = 0; i < ui.TryoutEases.Length && i < AnimatorKeyTryoutCommands.Offered.Length; i++)
        {
            Entity button = ui.TryoutEases[i];
            if (button.IsNull || !button.HasComponent<ButtonVisual>())
                continue;
            Color color = AnimatorKeyTryoutCommands.Offered[i] == picked ? Palette.Primary : UiTheme.Button;
            ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
            if (visual.Normal == color)
                continue;
            visual.Normal = color;
            visual.Hover = color.Lerp(Colors.White, 0.16f);
        }
    }
}
