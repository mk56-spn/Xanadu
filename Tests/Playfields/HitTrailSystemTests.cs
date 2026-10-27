using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Tests.Support;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class HitTrailSystemTests : EcsSystemTest
{
    [Test]
    public void TrailAgesAndIsRemoved()
    {
        Entity trail = Store.CreateEntity(
            new HitTrail(Vector2.Zero, Colors.Cyan, "S", lifetime: 0.1f));
        Root.Add(new HitTrailSystem());

        Frame(1, 0.05f);
        Assert.That(trail.GetComponent<HitTrail>().Age, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(trail.IsNull, Is.False);

        Frame(1, 0.06f);
        Assert.That(Store.Query<HitTrail>().Count, Is.Zero);
    }
}
