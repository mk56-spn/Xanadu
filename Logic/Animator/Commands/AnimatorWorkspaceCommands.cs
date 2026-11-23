using System;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Moves between the three workspaces (<see cref="AnimatorWorkspace"/>), leaving the one it was in.</summary>
public static class AnimatorWorkspaceCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<SetWorkspace>((s, e) => Set(s, e.Workspace));
    }

    public static AnimatorWorkspace Current(Entity session) =>
        AnimatorPreviewMode.IsOn(session) ? AnimatorWorkspace.Preview
        : AnimatorModes.Of(session).Rest ? AnimatorWorkspace.Rig
        : AnimatorWorkspace.Animate;

    public static void Set(Entity session, AnimatorWorkspace workspace)
    {
        if (Current(session) == workspace)
            return;

        if (workspace != AnimatorWorkspace.Preview)
            AnimatorPreviewModeCommands.Set(session, false);

        bool rest = workspace == AnimatorWorkspace.Rig;
        if (session.Tags.Has<RestPose>() != rest)
        {
            session.SetTag<RestPose>(rest);
            AnimatorSessionCommands.AnnounceMode(session, AnimatorMode.Rest, rest);
        }

        if (workspace == AnimatorWorkspace.Preview)
            AnimatorPreviewModeCommands.Set(session, true);
    }
}
