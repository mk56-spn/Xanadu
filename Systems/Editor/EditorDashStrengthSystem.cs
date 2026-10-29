using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>Exclusive 1×/2×/3× dash strength buttons for newly placed movement notes.</summary>
public sealed class EditorDashStrengthSystem : QuerySystem<EditorDashStrengthState>
{
    private static readonly Color Selected = Palette.Primary;
    private static readonly Color Normal = Palette.Muted;

    private readonly EntityStore _entities;

    public EditorDashStrengthSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorDashStrengthState>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorDashStrengthState state =
            ref editor.GetComponent<EditorDashStrengthState>();
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity entity in _entities.Query<EditorDashStrengthButton>().ToEntityList())
        {
            if (entity.Tags.Has<Pressed>())
                state.Strength = entity.GetComponent<EditorDashStrengthButton>().Strength;

            SetColor(
                entity,
                entity.GetComponent<EditorDashStrengthButton>().Strength == state.Strength,
                buffer);
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
