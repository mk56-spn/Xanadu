using System;
using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Xanadu.Particles;

namespace Xanadu.Tests.Particles;

[TestFixture]
public sealed class ParticleSpawnTests
{
    private readonly ParticleSeed[] _seeds = new ParticleSeed[XpfParticle.MaxAmount];

    private int Burst(XpfParticle effect, Vector2 position = default, float angle = 0f, Vector2 heading = default, Vector2 area = default) =>
        ParticleSpawn.Burst(effect, position, Colors.White, area, angle, heading, cells: 2, new Random(3), _seeds);

    [Test]
    public void ABurstThrowsTheEffectsAmount()
    {
        Assert.That(Burst(new XpfParticle { Amount = 24 }), Is.EqualTo(24));
    }

    [Test]
    public void AnExplosiveBurstIsBornAtOnceAndAGentleOneOverItsLifetime()
    {
        int count = Burst(new XpfParticle { Amount = 10, Explosiveness = 1f });
        for (int i = 0; i < count; i++)
            Assert.That(_seeds[i].Delay, Is.Zero);

        count = Burst(new XpfParticle { Amount = 10, Explosiveness = 0f, Lifetime = 2f });
        Assert.That(_seeds[0].Delay, Is.Zero);
        Assert.That(_seeds[count - 1].Delay, Is.EqualTo(1.8f).Within(1e-5f));
    }

    [Test]
    public void LifetimesShortenOnlyAsFarAsTheRandomnessAllows()
    {
        int count = Burst(new XpfParticle { Amount = 200, Lifetime = 1f, Randomness = 0.25f });
        for (int i = 0; i < count; i++)
            Assert.That(_seeds[i].Lifespan, Is.InRange(0.75f, 1f));
    }

    [Test]
    public void ParticlesFlyInsideTheSpreadConeTurnedWithTheEmitter()
    {
        var effect = new XpfParticle { Amount = 200, Direction = 0f, Spread = 10f, SpeedMin = 100f, SpeedMax = 200f };
        int count = Burst(effect, angle: Mathf.Pi / 2f);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = _seeds[i].Velocity;
            Assert.That(velocity.Length(), Is.InRange(100f - 1e-3f, 200f + 1e-3f));
            Assert.That(Mathf.RadToDeg(Mathf.Abs(velocity.AngleTo(Vector2.Down))), Is.LessThanOrEqualTo(10f + 1e-3f));
        }
    }

    [Test]
    public void AHeadingReplacesTheEffectsDirectionAndSpread()
    {
        var effect = new XpfParticle { Amount = 200, Direction = -90f, Spread = 180f, HeadedSpread = 5f };
        int count = Burst(effect, angle: 1f, heading: new Vector2(-3f, 0f));
        for (int i = 0; i < count; i++)
            Assert.That(Mathf.RadToDeg(Mathf.Abs(_seeds[i].Velocity.AngleTo(Vector2.Left))), Is.LessThanOrEqualTo(5f + 1e-3f));
    }

    [Test]
    public void APointEffectStartsOnTheSpotAndABoxInsideTheBurstsArea()
    {
        var at = new Vector2(50f, 60f);
        int count = Burst(new XpfParticle { Amount = 50 }, at, area: new Vector2(100f, 100f));
        for (int i = 0; i < count; i++)
            Assert.That(_seeds[i].Position, Is.EqualTo(at));

        count = Burst(new XpfParticle { Amount = 200, Shape = XpfShape.Box }, at, area: new Vector2(40f, 10f));
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = _seeds[i].Position - at;
            Assert.That(Mathf.Abs(offset.X), Is.LessThanOrEqualTo(20f));
            Assert.That(Mathf.Abs(offset.Y), Is.LessThanOrEqualTo(5f));
        }
    }

    [Test]
    public void AnUprightEffectIgnoresTheEmittersTurn()
    {
        Burst(new XpfParticle { Amount = 1, Upright = true, AngleMin = 30f, AngleMax = 30f }, angle: 1f);
        Assert.That(_seeds[0].Angle, Is.EqualTo(Mathf.DegToRad(30f)).Within(1e-5f));

        Burst(new XpfParticle { Amount = 1, AngleMin = 30f, AngleMax = 30f }, angle: 1f);
        Assert.That(_seeds[0].Angle, Is.EqualTo(1f + Mathf.DegToRad(30f)).Within(1e-5f));
    }

    [Test]
    public void NoteGlyphsPickACellAndSoftDotsNone()
    {
        int count = Burst(new XpfParticle { Amount = 100, Texture = XpfTexture.NoteGlyphs });
        var cells = new HashSet<int>();
        for (int i = 0; i < count; i++)
            cells.Add(_seeds[i].Cell);
        Assert.That(cells, Is.EquivalentTo(new[] { 0, 1 }));

        Burst(new XpfParticle { Amount = 1 });
        Assert.That(_seeds[0].Cell, Is.EqualTo(-1));
    }

    [Test]
    public void TheScaleCurveEasesBetweenPointsAndHoldsPastItsEnds()
    {
        var effect = new XpfParticle();
        Assert.That(ParticleSpawn.Scale(effect, 0.5f), Is.EqualTo(1f));

        effect.ScaleCurve.Add(new XpfCurvePoint(0.2f, 0f));
        effect.ScaleCurve.Add(new XpfCurvePoint(0.6f, 1f));
        Assert.That(ParticleSpawn.Scale(effect, 0f), Is.Zero);
        Assert.That(ParticleSpawn.Scale(effect, 0.4f), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(ParticleSpawn.Scale(effect, 0.3f), Is.LessThan(0.25f));
        Assert.That(ParticleSpawn.Scale(effect, 1f), Is.EqualTo(1f));
    }

    [Test]
    public void TheRampBlendsLinearlyBetweenStops()
    {
        var effect = new XpfParticle();
        Assert.That(ParticleSpawn.Ramp(effect, 0.5f), Is.EqualTo(Colors.White));

        effect.Ramp.Add(new XpfColorStop(0f, new Color(1f, 1f, 1f, 1f)));
        effect.Ramp.Add(new XpfColorStop(1f, new Color(1f, 0f, 0f, 0f)));
        Color middle = ParticleSpawn.Ramp(effect, 0.25f);
        Assert.That(middle.G, Is.EqualTo(0.75f).Within(1e-5f));
        Assert.That(middle.A, Is.EqualTo(0.75f).Within(1e-5f));
    }

    [Test]
    public void AStreamThrowsWhatCameDueAllBornNow()
    {
        var effect = new XpfParticle { Amount = 30, Lifetime = 1.5f, Explosiveness = 0f };
        Assert.That(ParticleSpawn.StreamRate(effect), Is.EqualTo(20f));

        int count = ParticleSpawn.Stream(effect, 5, Vector2.Zero, Colors.White, 0f, 1, new Random(1), _seeds);
        Assert.That(count, Is.EqualTo(5));
        for (int i = 0; i < count; i++)
            Assert.That(_seeds[i].Delay, Is.Zero);
    }
}
