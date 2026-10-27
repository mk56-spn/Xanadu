using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Gives blocks a static Box2D box, but only while a play scene is running.
/// The editor keeps blocks as plain geometry, so nothing can push them while
/// they are being placed and resized.
/// </summary>
public sealed class BlockBodySystem : QuerySystem<Block>
{
    private readonly Box2DPhysicsWorld _physics;
    private readonly EntityStore _entities;

    public BlockBodySystem(Box2DPhysicsWorld physics, EntityStore entities)
    {
        _physics = physics;
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        // Blocks live in playfield units, the physics world in screen pixels,
        // so the body is built through the play field's view.
        if (!TryFindView(out PlayfieldView view))
            return;

        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref block, entity) =>
        {
            // A block a group moves takes its collision box with it.
            if (entity.HasComponent<PhysicsBody>())
            {
                if (entity.HasComponent<GroupBase>())
                {
                    entity.GetComponent<PhysicsBody>().Body.SetTransform(
                        Box2DPhysicsWorld.ToPhysics(view.PlayfieldToScreen(block.Center)),
                        0f);
                }

                return;
            }

            if (block.Size.X <= 0 || block.Size.Y <= 0)
                return;

            Body body = _physics.CreateStaticBox(
                view.PlayfieldToScreen(block.Center),
                block.Size * view.Scale);
            buffer.AddComponent(entity.Id, new PhysicsBody(body));
        });
    }

    /// <summary>The view of the playfield being played, if any; the editor only edits.</summary>
    private bool TryFindView(out PlayfieldView view)
    {
        foreach (Entity entity in _entities
                     .Query<PlayfieldView, PlayfieldMode>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            view = entity.GetComponent<PlayfieldView>();
            return true;
        }

        view = default;
        return false;
    }
}
