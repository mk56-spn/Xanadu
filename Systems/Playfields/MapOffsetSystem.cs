using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Shift and the left and right arrows change the map's offset during play, in the
/// direction of the global one: if hits read late, Shift+Right moves them back to
/// centre. Replays carry the offsets they were played with, so keys do nothing there.
/// </summary>
public sealed class MapOffsetSystem(
    IKeyboardState keyboard,
    MapOffsetSettings maps,
    OffsetSettings global)
    : QuerySystem<PlayOffset>
{
    /// <summary>How long the readout stays after a change.</summary>
    public const float LabelSeconds = 1.5f;

    private readonly KeyRepeat _repeat = new();

    protected override void OnUpdate()
    {
        int direction = 0;
        if (keyboard.IsPressed(Key.Shift))
            direction = (keyboard.IsPressed(Key.Right) ? 1 : 0) - (keyboard.IsPressed(Key.Left) ? 1 : 0);

        int steps = _repeat.Update(direction, Tick.deltaTime);
        foreach (Entity entity in Query.ToEntityList())
        {
            ref PlayOffset offset = ref entity.GetComponent<PlayOffset>();
            if (!offset.Loaded)
            {
                offset.Loaded = true;
                offset.MapMs = maps.Get(offset.LevelId);
            }

            if (steps > 0 && !entity.HasComponent<ReplayPlayer>())
            {
                offset.MapMs = maps.Set(offset.LevelId, offset.MapMs + direction * steps);
                offset.LabelSeconds = LabelSeconds;
                UiText.Set(
                    offset.Label,
                    $"MAP OFFSET {offset.MapMs:+0;-0;0} ms   ·   TOTAL {global.OffsetMs + offset.MapMs:+0;-0;0} ms");
            }

            offset.LabelSeconds = Mathf.Max(0f, offset.LabelSeconds - Tick.deltaTime);
            UiLayout.SetHidden(offset.Label, offset.LabelSeconds <= 0f);
        }
    }
}
