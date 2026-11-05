using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Exclusive 1×/2×/3× dash strength buttons for newly placed movement notes.</summary>
public sealed class EditorDashStrengthSystem : QuerySystem<EditorPlacement>
{
    private readonly EntityStore _entities;

    public EditorDashStrengthSystem(EntityStore entities)
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
            EditorButtonKind.DashStrength,
            placement => (int)placement.Strength,
            (placement, arg) =>
            {
                placement.Strength = (DashStrength)arg;
                return placement;
            });
    }
}
