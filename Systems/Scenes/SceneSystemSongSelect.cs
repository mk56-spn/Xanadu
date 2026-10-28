using Friflo.Engine.ECS.Systems;
using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using SceneState = Xanadu.Ecs.SceneState;

namespace Xanadu.Systems.Scenes;

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
    public bool StepSongSelect(int direction) =>
        CurrentController is SongSelectController controller
            ? controller.TryStep(direction)
            : false;

    public bool PlaySelected() =>
        CurrentController?.TryForward(this) ?? false;

    public bool EditSelected()
    {
        if (_stateEntity.IsNull)
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        string path = state.SelectedLevelPath;
        LevelEntry? entry = LoadEntry(path);

        // An empty path means there are no levels at all; the editor starts a
        // new one. A path that fails to load is an error, not a new level.
        if (entry is null && !string.IsNullOrWhiteSpace(path))
            return false;

        _pendingEditorEntry = entry;
        state.Pending = SceneKind.Editor;
        return true;
    }

}
