using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Particles;
using Writer.Tests.Support;

namespace Writer.Tests.Particles;

[TestFixture]
public sealed class ParticleBurstSystemTests : EcsSystemTest
{
    private sealed class FakeEmitter : IParticleEmitter
    {
        public List<(Vector2 Position, ParticleKind Kind)> Bursts { get; } = new();

        public void Burst(Vector2 position, ParticleKind kind) => Bursts.Add((position, kind));
    }

    private FakeEmitter _emitter = null!;

    protected override void OnEcsSetUp()
    {
        _emitter = new FakeEmitter();
        Root.Add(new ParticleBurstSystem(_emitter));
    }

    [Test]
    public void ARequestIsPlayedOnceAndRemoved()
    {
        Store.CreateEntity(new ParticleBurst(new Vector2(10, 20), ParticleKind.Landing));

        Frame(3);

        Assert.That(_emitter.Bursts, Is.EqualTo(new[] { (new Vector2(10, 20), ParticleKind.Landing) }));
        Assert.That(Store.Query<ParticleBurst>().Count, Is.EqualTo(0));
    }

    [Test]
    public void EveryRequestInAFrameIsPlayedWithItsOwnKind()
    {
        Store.CreateEntity(new ParticleBurst(new Vector2(1, 2), ParticleKind.Dash));
        Store.CreateEntity(new ParticleBurst(new Vector2(3, 4), ParticleKind.Landing));

        Frame();

        Assert.That(_emitter.Bursts, Has.Count.EqualTo(2));
        Assert.That(_emitter.Bursts, Contains.Item((new Vector2(1, 2), ParticleKind.Dash)));
        Assert.That(_emitter.Bursts, Contains.Item((new Vector2(3, 4), ParticleKind.Landing)));
    }
}
