using Friflo.Engine.ECS.Systems;
using Writer.Systems.Physics;

namespace Writer.Systems.Groups;

/// <summary>Applies character momentum and advances the external Box2D world.</summary>
public sealed class PhysicsSystemGroup : SystemGroup
{
    public PhysicsSystemGroup(Box2DPhysicsWorld physics) : base("Physics")
    {
        Add(new CharacterMomentumSystem());
        Add(new PhysicsStepSystem(physics));
    }
}
