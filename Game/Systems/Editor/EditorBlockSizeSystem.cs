using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Exclusive ½×½/1×1/2×1 size buttons for the blocks the block tool places.</summary>
public sealed class EditorBlockSizeSystem : QuerySystem<EditorPlacement>
{
    private readonly EntityStore _entities;

    public EditorBlockSizeSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement>(_entities);
        if (editor.IsNull)
            return;

        EditorButtons.Exclusive(
            editor,
            _entities,
            CommandBuffer,
            EditorButtonKind.BlockSize,
            placement => (int)placement.BlockSize,
            (placement, arg) =>
            {
                placement.BlockSize = (BlockBrushSize)arg;
                return placement;
            });
    }
}
