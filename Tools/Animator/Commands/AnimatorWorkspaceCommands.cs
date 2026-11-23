using System;
using Friflo.Engine.ECS;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Moves between the three workspaces (<see cref="AnimatorWorkspace"/>), leaving the one it was in.</summary>
public static class AnimatorWorkspaceCommands
{
    public static AnimatorWorkspace Current(Entity session) =>
        AnimatorPreviewModeCommands.IsOn(session) ? AnimatorWorkspace.Preview
        : session.GetComponent<AnimatorModes>().Rest ? AnimatorWorkspace.Rig
        : AnimatorWorkspace.Animate;

    public static void Set(Entity session, string argument)
    {
        if (Enum.TryParse(argument, ignoreCase: true, out AnimatorWorkspace workspace))
            Set(session, workspace);
    }

    public static void Set(Entity session, AnimatorWorkspace workspace)
    {
        if (Current(session) == workspace)
            return;

        if (workspace != AnimatorWorkspace.Preview)
            AnimatorPreviewModeCommands.Set(session, false);

        ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
        bool rest = workspace == AnimatorWorkspace.Rig;
        if (modes.Rest != rest)
        {
            modes.Set(AnimatorMode.Rest, rest);
            AnimatorModeSystem.Announce(session, AnimatorMode.Rest, rest);
        }

        if (workspace == AnimatorWorkspace.Preview)
            AnimatorPreviewModeCommands.Set(session, true);
    }
}
