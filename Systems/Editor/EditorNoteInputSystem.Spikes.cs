using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Spike tile painting and picking. Split out of the note input system
/// because spikes are grid geometry, not timed items.
/// </summary>
public sealed partial class EditorNoteInputSystem
{
    /// <summary>The spike tile under a playfield point, or null.</summary>
    private Entity FindSpikeAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (entity.GetComponent<Spike>().Bounds.HasPoint(position))
                result = entity;
        }

        return result;
    }

    /// <summary>
    /// Adds the tile under the pointer unless that cell already holds one, so
    /// dragging across cells paints a run of spikes.
    /// </summary>
    private void PaintSpikeAt(Vector2 position, Playfield playfield)
    {
        if (!IsInside(position, playfield))
            return;

        Spike spike = SpikeRules.TileAt(position, playfield);
        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (entity.GetComponent<Spike>().Bounds.Intersects(spike.Bounds))
                return;
        }

        EditorItems.Create(_entities, spike);
        _spikeStrokeAdded = true;
    }

    private static bool IsSpikeMode(Entity editor) =>
        editor.HasComponent<EditorPlacementModeState>() &&
        editor.GetComponent<EditorPlacementModeState>().Mode ==
        EditorPlacementMode.Spikes;
}
