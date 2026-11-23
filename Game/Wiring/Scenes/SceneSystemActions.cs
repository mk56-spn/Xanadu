using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void HandleActions(ref SceneState state)
    {
        CurrentController?.HandleActions(this, ref state);
        if (state.Pending != state.Current)
            return;

        foreach ((Entity entity, SceneActionId action, _) in UiCommand.All<SceneActionId>(_entities))
        {
            if (!entity.Tags.Has<Pressed>())
                continue;

            var context = new SceneActionContext(
                this,
                _entities,
                entity,
                action);
            if (_actionRegistry.TryInvoke(state.Current, context))
            {
                entity.RemoveTag<Pressed>();
                return;
            }
        }

        foreach ((Entity entity, SceneKind target, _) in UiCommand.All<SceneKind>(_entities))
        {
            if (!entity.Tags.Has<Pressed>() || target == SceneKind.None)
                continue;

            // Timing needs a song already chosen (song select chooses it).
            if (target == SceneKind.SongTiming && string.IsNullOrWhiteSpace(state.SelectedSongId))
                continue;

            state.Pending = target;
            return;
        }

    }
}
