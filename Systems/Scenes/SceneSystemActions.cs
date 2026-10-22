using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Profiles;
using Writer.Scenes;
using Writer.Settings;
using SceneState = Writer.Ecs.SceneState;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void HandleActions(ref SceneState state)
    {
        CurrentController?.HandleActions(this, ref state);
        if (state.Pending != state.Current)
            return;

        if (state.Current == SceneKind.Play)
        {
            foreach (Entity entity in _entities
                         .Query<PauseAction>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                ShowPause();
                return;
            }
        }

        foreach (Entity entity in _entities
                     .Query<RetryAction>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            if (state.Current == SceneKind.Results)
                RetryPlay(ref state);

            return;
        }

        foreach (Entity entity in _entities
                     .Query<SceneAction>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            SceneKind target = entity.GetComponent<SceneAction>().Target;
            if (target == SceneKind.None)
                continue;

            if (target == SceneKind.Editor &&
                entity.HasComponent<EditLevelAction>())
            {
                _pendingEditorEntry = LoadEntry(state.SelectedLevelPath);
            }

            state.Pending = target;
            return;
        }

    }
}
