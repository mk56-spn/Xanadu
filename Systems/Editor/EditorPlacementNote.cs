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
    /// <summary>True for a Form step with no shape chosen, which cannot be placed.</summary>
    public static bool NeedsShape(Entity editor) =>
        editor.TryGetComponent(out EditorPlacement placement) &&
        placement.Mode == EditorPlacementMode.FormStep &&
        placement.ShapeId == 0;

    public static Note Build(Entity editor, long time, Vector2 position)
    {
        EditorPlacement placement = editor.TryGetComponent(out EditorPlacement found)
            ? found
            : new EditorPlacement { Direction = DashDirection.Up };
        KeybindAction target = placement.Note;
        CharacterKind character = placement.Character;
        EditorPlacementMode mode = placement.Mode;
        DashDirection direction = placement.Direction;
        DashStrength strength = placement.Strength;
        bool isPair = mode == EditorPlacementMode.Pair;
        bool isContact = mode == EditorPlacementMode.Contact;
        bool isChannel = mode == EditorPlacementMode.Channel;
        bool isFormStep = mode == EditorPlacementMode.FormStep;
        KeybindAction channelTarget = target == KeybindAction.M
            ? KeybindAction.L1
            : target;

        return new Note(
            time,
            position,
            target: mode == EditorPlacementMode.CharacterSwitch ||
                    mode == EditorPlacementMode.WeaponSwitch ||
                    isPair ||
                    isContact
                ? KeybindAction.M
                : isChannel || isFormStep
                    ? channelTarget
                    : target,
            kind: mode switch
            {
                EditorPlacementMode.Pair => NoteKind.Pair,
                EditorPlacementMode.Dash => NoteKind.Dash,
                EditorPlacementMode.Momentum => NoteKind.Momentum,
                EditorPlacementMode.Contact => NoteKind.Contact,
                EditorPlacementMode.CharacterSwitch => NoteKind.CharacterSwitch,
                EditorPlacementMode.WeaponSwitch => NoteKind.WeaponSwitch,
                EditorPlacementMode.Channel => NoteKind.Channel,
                EditorPlacementMode.FormStep => NoteKind.ConstructionStep,
                _ => NoteKind.Hit,
            },
            dashDirection: direction,
            contactRequired: isContact ? 0 : 1,
            character: character,
            dashStrength: strength,
            pairIndex: NoteRules.PairIndexFor(target),
            momentum: MomentumFor(strength),
            contactLeft: isContact ? placement.ContactLeft : 0,
            contactRight: isContact ? placement.ContactRight : 0,
            channelSide: isChannel
                ? NoteRules.SideOf(channelTarget)
                : ChannelSide.Left,
            shapeId: isFormStep ? placement.ShapeId : 0,
            shapePiece: 0,
            exitDirection: placement.ExitDirection,
            exitMomentum: mode == EditorPlacementMode.Dash ? placement.ExitMomentum : 0f);
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
