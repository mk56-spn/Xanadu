using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The note the current tool places, from the editor's tool state. Placing and
/// the ghost under the pointer share it, so the ghost always tells the truth.
/// </summary>
public static class EditorPlacementNote
{
    public static Note Build(Entity editor, long time, Vector2 position)
    {
        EditorPlacement placement = editor.TryGetComponent(out EditorPlacement found)
            ? found
            : new EditorPlacement();
        EditorPlacementMode mode = placement.Mode;
        bool directional = mode is EditorPlacementMode.Dash or EditorPlacementMode.Momentum;

        return new Note(
            time,
            position,
            target: placement.Note,
            kind: mode switch
            {
                EditorPlacementMode.Neutral => NoteKind.Neutral,
                EditorPlacementMode.Dash => NoteKind.Dash,
                EditorPlacementMode.Momentum => NoteKind.Momentum,
                EditorPlacementMode.Contact => NoteKind.Contact,
                _ => NoteKind.Hit,
            },
            dashStrength: placement.Strength,
            momentum: MomentumFor(placement.Strength),
            diagonal: directional && placement.Diagonal,
            costs: mode == EditorPlacementMode.Contact ? placement.Costs : default);
    }

    /// <summary>
    /// The three movement strength buttons pick a dash distance and a momentum
    /// push, so a mapper gets a consistent 1×/2×/3× language.
    /// </summary>
    private static float MomentumFor(DashStrength strength) => strength switch
    {
        DashStrength.Double => 900f,
        DashStrength.Triple => Box2DPhysicsWorld.MaxMomentum,
        _ => 600f,
    };
}
