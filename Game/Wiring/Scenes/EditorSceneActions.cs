using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Wiring.Scenes;

/// <summary>Action registration for the level editor.</summary>
public static class EditorSceneActions
{
    public static void Register(SceneActionRegistry registry)
    {
        registry.Register(SceneKind.Editor, SceneActionId.Exit,
            context => context.Scenes.TryBack());

        registry.Register(SceneKind.Editor, SceneActionId.Save,
            context => AskEditor(context, new SaveLevel()));

        registry.Register(SceneKind.Editor, SceneActionId.LoadSong,
            context => AskEditor(context, new OpenSongPicker()));

        registry.Register(SceneKind.Editor, SceneActionId.ToggleMetadata,
            context => AskEditor(context, new ToggleLevelInfo()));
    }

    private static void AskEditor<T>(SceneActionContext context, T intent)
        where T : struct, IIntent<T>
    {
        EntityList editors = context.Entities.Query<LevelInfo>().ToEntityList();
        if (editors.Count > 0)
            editors[0].EmitSignal(intent);
    }
}
