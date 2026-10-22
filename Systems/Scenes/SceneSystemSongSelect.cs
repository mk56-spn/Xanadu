using Friflo.Engine.ECS.Systems;
using System;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using SceneState = Writer.Ecs.SceneState;

namespace Writer.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    public bool TryStepCarousel(int direction) =>
        _controllers.Get(SceneKind.SongSelect) is SongSelectController songSelect &&
        songSelect.TryStep(direction);

    private LevelEntry? LoadEntry(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return new LevelEntry(path, LevelStore.Load(path));
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to load level '{path}': {exception.Message}");
            return null;
        }
    }
}
