using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class DashTrailSystemTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new DashTrailSystem());
    }

    [Test]
    public void DashTrailFadesAndIsRemoved()
    {
        Entity trail = Store.CreateEntity();
        trail.AddComponent(new DashTrail(
            new Godot.Vector2(0, 0),
            new Godot.Vector2(100, 100),
            lifetime: 0.1f));

        Frame(1, 0.05f);
        Assert.That(Store.Query<DashTrail>().Count, Is.EqualTo(1));

        Frame(1, 0.06f);
        Assert.That(Store.Query<DashTrail>().Count, Is.Zero);
    }
}
