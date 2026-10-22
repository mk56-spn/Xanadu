using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Exclusive note/mine/movement placement mode and dash-direction wheel.</summary>
public sealed class EditorPlacementSystem : QuerySystem<EditorPlacementModeState>
{
    private static readonly Color Selected = new("#2563eb");
    private static readonly Color Normal = new("#334155");
    private readonly EntityStore _entities;

    public EditorPlacementSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorPlacementModeState, EditorDashDirectionState>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorPlacementModeState mode =
            ref editor.GetComponent<EditorPlacementModeState>();
        ref EditorDashDirectionState direction =
            ref editor.GetComponent<EditorDashDirectionState>();
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity entity in _entities.Query<EditorPlacementButton>().ToEntityList())
        {
            if (entity.Tags.Has<Pressed>())
                mode.Mode = entity.GetComponent<EditorPlacementButton>().Mode;
            SetColor(entity, entity.GetComponent<EditorPlacementButton>().Mode == mode.Mode, buffer);
        }

        foreach (Entity entity in _entities.Query<EditorDashDirectionButton>().ToEntityList())
        {
            if (entity.Tags.Has<Pressed>())
                direction.Direction = entity.GetComponent<EditorDashDirectionButton>().Direction;
            SetColor(entity, entity.GetComponent<EditorDashDirectionButton>().Direction == direction.Direction, buffer);
        }
    }

    private static void SetColor(Entity entity, bool selected, CommandBuffer buffer)
    {
        if (!entity.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref entity.GetComponent<ButtonVisual>();
        Color normal = selected ? Selected : Normal;
        if (visual.Normal == normal)
            return;

        visual.Normal = normal;
        visual.Hover = normal.Lerp(Colors.White, 0.16f);
        visual.Flash = Colors.White;
        buffer.AddTag<Dirty>(entity.Id);
    }
}
