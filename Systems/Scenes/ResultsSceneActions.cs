using Xanadu.Ecs;

namespace Xanadu.Systems.Scenes;

/// <summary>Action registration for the results screen.</summary>
public static class ResultsSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.Results, SceneActionId.Retry,
            context => context.Scenes.RetryPlay());

        registry.Register(SceneKind.Results, SceneActionId.WatchReplay,
            context => context.Scenes.WatchLastReplay());

        registry.Register(SceneKind.Results, SceneActionId.SongSelect,
            context => context.Scenes.Request(SceneKind.SongSelect));

        registry.Register(SceneKind.Results, SceneActionId.MainMenu,
            context => context.Scenes.Request(SceneKind.Main));
    }
}
