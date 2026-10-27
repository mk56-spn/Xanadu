using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Scenes;
using Xanadu.Settings;
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void HandleActions(ref SceneState state)
    {
        CurrentController?.HandleActions(this, ref state);
        if (state.Pending != state.Current)
            return;

        foreach (Entity entity in _entities
                     .Query<SceneCommand>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            var context = new SceneActionContext(
                this,
                _entities,
                entity,
                entity.GetComponent<SceneCommand>());
            if (_actionRegistry.TryInvoke(state.Current, context))
            {
                entity.RemoveTag<Pressed>();
                return;
            }
        }

        foreach (Entity entity in _entities
                     .Query<SceneAction>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            SceneKind target = entity.GetComponent<SceneAction>().Target;
            if (target == SceneKind.None)
                continue;

            if (target == SceneKind.SongTiming)
            {
                foreach (Entity editor in _entities.Query<LevelInfo>().ToEntityList())
                {
                    state.SelectedSongId = editor.GetComponent<LevelInfo>().SongId;
                    break;
                }

                if (string.IsNullOrWhiteSpace(state.SelectedSongId))
                    continue;
            }

            state.Pending = target;
            return;
        }

    }
}
