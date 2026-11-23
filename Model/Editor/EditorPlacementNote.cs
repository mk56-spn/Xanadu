using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Editor;

/// <summary>
/// The note the current tool places, from the editor's tool state. Placing and
/// the ghost under the pointer share it, so the ghost always tells the truth.
/// </summary>
public static class EditorPlacementNote
{
    /// <summary>The tool's note at a time and place.</summary>
    public static Note Build(Entity editor, long time, Vector2 position)
    {
        EditorPlacement placement = editor.TryGetComponent(out EditorPlacement found)
            ? found
            : new EditorPlacement();
        NoteKind kind = placement.Mode switch
        {
            EditorPlacementMode.Dash => NoteKind.Dash,
            EditorPlacementMode.Entrance => NoteKind.Entrance,
            EditorPlacementMode.Orb => NoteKind.Orb,
            _ => NoteKind.Tap,
        };
        return new Note(time, position, placement.Note, kind);
    }

    /// <summary>
    /// Whether the tool could place right now: there is a song to time it to and
    /// the level is not full. The ghost shows red when not, as osu!'s placements do.
    /// </summary>
    public static bool CanPlace(Entity editor, EntityStore entities) =>
        editor.TryGetComponent(out LevelInfo level) && level.Song is not null &&
        !EditorItems.IsFull(entities);
}
