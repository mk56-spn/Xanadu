using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Specialises the generic wheel for dash directions: selects the wedge from
/// <see cref="EditorDashDirectionState"/>, reflects user selections back into
/// that state, and hides the wheel and its label outside the movement modes.
/// </summary>
public sealed class EditorDirectionWheelSystem : QuerySystem<EditorDirectionWheel>
{
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

            EditorPlacementMode mode =
                entity.HasComponent<EditorPlacementModeState>()
                    ? entity.GetComponent<EditorPlacementModeState>().Mode
                    : EditorPlacementMode.Notes;
            bool visible = mode is
                EditorPlacementMode.Dash or
                EditorPlacementMode.Momentum;

            UiLayout.SetHidden(link.Wheel, buffer, !visible);
            UiLayout.SetHidden(link.Label, buffer, !visible);

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
}
