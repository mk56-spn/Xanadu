using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Specialises the generic wheel for dash directions: selects the wedge from
/// <see cref="EditorDashDirectionState"/>, reflects user selections back into
/// that state, and moves the floating wheel off-screen outside MOVE mode.
/// </summary>
public sealed class EditorDirectionWheelSystem : QuerySystem<EditorDirectionWheel>
{
    private const float LabelGap = 16f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity entity in Query.ToEntityList())
        {
            EditorDirectionWheel link = entity.GetComponent<EditorDirectionWheel>();
            if (link.Wheel.IsNull || !link.Wheel.HasComponent<UiWheel>())
                continue;

            if (entity.HasComponent<EditorDashDirectionState>() &&
                link.Wheel.HasComponent<UiWheelChanged>())
            {
                int index = link.Wheel.GetComponent<UiWheelChanged>().Index;
                entity.GetComponent<EditorDashDirectionState>().Direction =
                    DashDirectionWheel.At(index);
                link.Wheel.RemoveComponent<UiWheelChanged>();
            }

            bool visible =
                entity.HasComponent<EditorPlacementModeState>() &&
                entity.GetComponent<EditorPlacementModeState>().Mode ==
                EditorPlacementMode.Movement;

            float radius = link.Wheel.HasComponent<Rectangle>()
                ? link.Wheel.GetComponent<Rectangle>().Bounds.Size.X / 2
                : 0;
            Move(link.Wheel, visible ? link.Home : UiLayout.Hidden, buffer);
            Move(
                link.Label,
                visible
                    ? link.Home + new Vector2(0, -radius - LabelGap)
                    : UiLayout.Hidden,
                buffer);

            if (visible && entity.HasComponent<EditorDashDirectionState>())
            {
                int expected = DashDirectionWheel.IndexOf(
                    entity.GetComponent<EditorDashDirectionState>().Direction);
                UiWheelControl.SetSelected(
                    link.Wheel,
                    expected,
                    raiseChanged: false);
            }
        }
    }

    private static void Move(Entity entity, Vector2 target, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position position = ref entity.GetComponent<Position>();
        if (position.Value == target)
            return;

        position.Value = target;
        buffer.AddTag<Moved>(entity.Id);
    }
}
