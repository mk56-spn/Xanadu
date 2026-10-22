using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class HitTrailSystemTests
{
    [Test]
    public void TrailAgesAndIsRemoved()
    {
        var store = new EntityStore();
        Entity trail = store.CreateEntity(
            new HitTrail(Vector2.Zero, Colors.Cyan, "S", lifetime: 0.1f));
        var root = new SystemRoot(store);
        root.Add(new HitTrailSystem());

        root.Update(new UpdateTick(0.05f, 0));
        Assert.That(trail.GetComponent<HitTrail>().Age, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(trail.IsNull, Is.False);

        root.Update(new UpdateTick(0.06f, 0));
        Assert.That(store.Query<HitTrail>().Count, Is.Zero);
    }
}
