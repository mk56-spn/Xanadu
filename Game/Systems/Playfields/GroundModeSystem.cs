using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The two ground movement modes under test, switched with F8. Skid (the
/// default): the ground has no friction, so a landing keeps its sideways speed
/// until a friction field stops it. Hold: on the
/// ground the player keeps its sideways speed only while the direction key of
/// the last directional note it hit (<see cref="MoveKey"/>) is held; letting go
/// stops it at once.
/// </summary>
public sealed class GroundModeSystem(
    KeybindSettings keybinds,
    IKeyboardState keyboard,
    IOverlayToggleSource? toggle = null) : QuerySystem<PlayfieldMode, Player, PhysicsBody, Position>
{
    private readonly OverlayLatch? _latch = toggle is null ? null : new OverlayLatch(toggle);

    protected override void OnUpdate()
    {
        _latch?.Update();
        bool hold = _latch?.Visible == true;
        ShaderPlay.GroundHold = hold;
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref mode, ref player, ref body, ref position, entity) =>
        {
            if (mode.Kind != PlayfieldKind.Play)
                return;

            if (entity.TryGetComponent(out GroundModeHud hud))
                UiText.Set(hud.Label, hold ? "GROUND: HOLD  (F8)" : "GROUND: SKID  (F8)", buffer);

            if (!hold || entity.Tags.Has<PlayPaused>() || entity.HasComponent<DashHang>())
                return;

            if (!entity.TryGetComponent(out PlayfieldView view) ||
                !entity.TryGetComponent(out Playfield playfield) ||
                !PlayerGround.TryStanding(entity, view, playfield, position.Value, out _))
            {
                return;
            }

            bool held = entity.TryGetComponent(out MoveKey key) && keyboard.IsPressed(keybinds.Get(key.Key));
            Vector2 velocity = body.Body.LinearVelocity;
            if (!held && velocity.X != 0f)
                body.Body.SetLinearVelocity(new Vector2(0f, velocity.Y));
        });
    }
}
