using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>Exclusive ½×½/1×1/2×1 size buttons for the blocks the block tool places.</summary>
public sealed class EditorBlockSizeSystem : QuerySystem<EditorPlacement>
{
    private static readonly Color Selected = Palette.Primary;
    private static readonly Color Normal = Palette.Muted;

    private readonly EntityStore _entities;

    public EditorBlockSizeSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorPlacement>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorPlacement state = ref editor.GetComponent<EditorPlacement>();
        CommandBuffer buffer = CommandBuffer;

        foreach (Entity entity in EditorButton.All(_entities, EditorButtonKind.BlockSize))
        {
            var size = (BlockBrushSize)entity.GetComponent<EditorButton>().Arg;
            if (entity.Tags.Has<Pressed>())
                state.BlockSize = size;

            SetColor(entity, size == state.BlockSize, buffer);
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
