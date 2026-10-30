using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Keeps a rail hold alive while its key is down. Releasing early breaks the
/// hold; reaching its end clears it. The rail note itself was scored when the
/// hold began.
/// </summary>
public sealed class RailHoldSystem : QuerySystem<RailHold>
{
    private readonly IKeyboardState _keyboard;
    private readonly KeybindSettings _keybinds;

    public RailHoldSystem(IKeyboardState keyboard, KeybindSettings keybinds)
    {
        _keyboard = keyboard;
        _keybinds = keybinds;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref hold, entity) =>
        {
            Entity playfield = hold.Playfield;
            if (playfield.IsNull || !playfield.HasComponent<Playhead>())
            {
                buffer.DeleteEntity(entity.Id);
                return;
            }

            Playhead playhead = playfield.GetComponent<Playhead>();
            if (playhead.TimeMs >= hold.EndMs)
            {
                buffer.DeleteEntity(entity.Id);
                return;
            }

            if (_keyboard.IsPressed(_keybinds.Get(hold.Action)))
                return;

            if (playfield.HasComponent<ScoreState>())
            {
                ref ScoreState score = ref playfield.GetComponent<ScoreState>();
                PlayScoring.FailHold(
                    ref score,
                    RailPosition(playfield, hold, playhead.TimeMs),
                    playhead.TimeMs);
            }

            buffer.DeleteEntity(entity.Id);
        });
    }

    private static Vector2 RailPosition(
        Entity playfield,
        RailHold hold,
        long playheadMs)
    {
        if (!playfield.TryGetComponent(out PlayfieldView view) ||
            !playfield.TryGetComponent(out Playfield field))
        {
            return Vector2.Zero;
        }

        return SplitlineRail.ScreenPosition(
            view,
            field,
            hold.Side,
            hold.Index,
            hold.StartMs,
            playheadMs);
    }
}
