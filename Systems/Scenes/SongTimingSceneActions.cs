using Xanadu.Ecs;

namespace Xanadu.Systems.Scenes;

public static class SongTimingSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.SongTiming, SceneActionId.Back,
            context => context.Scenes.CloseSongTiming());
    }
}
