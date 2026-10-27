using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Groups;

/// <summary>Applies character momentum and advances the external Box2D world.</summary>
public sealed class PhysicsSystemGroup : SystemGroup
{
    public PhysicsSystemGroup(Box2DPhysicsWorld physics, EntityStore entities)
        : base("Physics")
    {
        Add(new BlockBodySystem(physics, entities));
        Add(new PlayerBodySystem(physics));
        Add(new CharacterMomentumSystem());
        Add(new PhysicsStepSystem(physics));
    }
}
