using Xanadu.Ecs;
using Xanadu.Ecs.Components;

namespace Xanadu.Systems.Scenes;

public static class SongTimingSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.SongTiming, SceneActionId.Back,
            context => context.Scenes.CloseSongTiming());

        registry.Register(SceneKind.SongTiming, SceneActionId.AddSong,
            context => context.Scenes.RequestAddSong());
    }
}
