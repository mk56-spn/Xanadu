using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Specialises the generic wheel for dash directions: selects the wedge from
/// <see cref="EditorDashDirectionState"/>, reflects user selections back into
/// that state, tints it with the colour of the note being placed, and hides the
/// wheel and its label outside the movement modes.
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

            if (visible && entity.HasComponent<EditorNoteTarget>())
            {
                Tint(
                    link.Wheel,
                    PlayfieldRenderer.ColorFor(entity.GetComponent<EditorNoteTarget>().Target),
                    buffer);
            }

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

    private static void Tint(Entity wheel, Color color, CommandBuffer buffer)
    {
        ref UiWheel data = ref wheel.GetComponent<UiWheel>();
        if (data.SelectedColor == color)
            return;

        Color dimmed = EditorNoteTypeBarSystem.Dimmed(color);
        data.SelectedColor = color;
        data.RimColor = dimmed;
        data.Colors = [dimmed];
        buffer.AddTag<Dirty>(wheel.Id);
    }
}
