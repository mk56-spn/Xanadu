using Friflo.Engine.ECS;
using Writer.Ecs;

namespace Writer.Systems.Scenes;

/// <summary>Action registration for the level editor.</summary>
public static class EditorSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.Editor, SceneActionId.Exit,
            context => context.Scenes.TryBack());

        registry.Register(SceneKind.Editor, SceneActionId.SongTiming,
            context => context.Scenes.OpenSongTiming());

        registry.Register(SceneKind.Editor, SceneActionId.Save,
            context => RequestEditorUi(context, EditorButtonKind.Save));

        registry.Register(SceneKind.Editor, SceneActionId.SaveAs,
            context => RequestEditorUi(context, EditorButtonKind.SaveAs));

        registry.Register(SceneKind.Editor, SceneActionId.LoadSong,
            context => RequestEditorUi(context, EditorButtonKind.LoadSong));

        registry.Register(SceneKind.Editor, SceneActionId.LoadLevel,
            context => RequestEditorUi(context, EditorButtonKind.LoadLevel));

        registry.Register(SceneKind.Editor, SceneActionId.ToggleMetadata,
            context => RequestEditorUi(context, EditorButtonKind.ToggleMetadata));
    }

    private static void RequestEditorUi(
        SceneActionContext context,
        EditorButtonKind kind)
    {
        foreach (Entity editor in context.Entities
                     .Query<LevelInfo>()
                     .ToEntityList())
        {
            editor.AddComponent(new EditorUiRequest(kind));
            return;
        }
    }
}
