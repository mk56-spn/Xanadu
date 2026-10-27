using Xanadu.Ecs;

namespace Xanadu.Systems.Scenes;

/// <summary>Action registration for play HUD and pause controls.</summary>
public static class PauseSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.Play, SceneActionId.Resume,
            context => context.Scenes.ResumePause());

        registry.Register(SceneKind.Play, SceneActionId.Retry,
            context => context.Scenes.RetryPlay());

        registry.Register(SceneKind.Play, SceneActionId.Leave,
            context => context.Scenes.LeavePause());

        registry.Register(SceneKind.Play, SceneActionId.Pause,
            context => context.Scenes.PausePlay());
    }
}
