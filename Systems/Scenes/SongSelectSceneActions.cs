using Writer.Ecs;

namespace Writer.Systems.Scenes;

/// <summary>Action registration for song select.</summary>
public static class SongSelectSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.SongSelect, SceneActionId.Play,
            context => context.Scenes.PlaySelected());

        registry.Register(SceneKind.SongSelect, SceneActionId.Edit,
            context => context.Scenes.EditSelected());

        registry.Register(SceneKind.SongSelect, SceneActionId.Previous,
            context => context.Scenes.StepSongSelect(-1));

        registry.Register(SceneKind.SongSelect, SceneActionId.Next,
            context => context.Scenes.StepSongSelect(1));
    }
}
