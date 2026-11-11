using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Exclusive note/mine/movement placement mode buttons.</summary>
public sealed class EditorPlacementSystem : QuerySystem<EditorPlacement>
{
    private readonly EntityStore _entities;

    public EditorPlacementSystem(EntityStore entities)
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
            EditorButtonKind.Place,
            placement => (int)placement.Mode,
            (placement, arg) =>
            {
                placement.Mode = (EditorPlacementMode)arg;
                return placement;
            });
    }
}
