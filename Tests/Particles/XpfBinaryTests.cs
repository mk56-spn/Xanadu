using System;
using System.IO;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Particles;

namespace Xanadu.Tests.Particles;

[TestFixture]
public sealed class XpfBinaryTests
{
    private const string LibraryFile = "Assets/Particles/particles.xpf";

    private static XpfParticle Sample() => new()
    {
        Name = "sparks",
        Texture = XpfTexture.NoteGlyphs,
        Amount = 40,
        Lifetime = 0.7f,
        Explosiveness = 0.3f,
        Randomness = 0.2f,
        Shape = XpfShape.Box,
        BoxSize = new Vector2(30, 4),
        Direction = 200f,
        Spread = 20f,
        HeadedSpread = 40f,
        SpeedMin = 100f,
        SpeedMax = 300f,
        Gravity = new Vector2(0, 900),
        DampingMin = 10f,
        DampingMax = 20f,
        ScaleMin = 0.5f,
        ScaleMax = 1.5f,
        ScaleCurve = { new XpfCurvePoint(0f, 1f), new XpfCurvePoint(0.5f, 0.8f), new XpfCurvePoint(1f, 0f) },
        AngleMin = -10f,
        AngleMax = 10f,
        Upright = true,
        Color = new Color(1f, 0.8f, 0.2f),
        Ramp = { new XpfColorStop(0f, Colors.White), new XpfColorStop(1f, new Color(1, 1, 1, 0)) },
        Reach = 500f,
    };

    [Test]
    public void ALibraryReadsBackAsItWasWritten()
    {
        var library = new XpfLibrary { Particles = { Sample(), new XpfParticle { Name = "plain" } } };

        XpfLibrary read = XpfBinary.Read(XpfBinary.Write(library));

        Assert.That(read.Particles, Has.Count.EqualTo(2));
        XpfParticle expected = library.Particles[0];
        XpfParticle actual = read.Particles[0];
        foreach (var field in typeof(XpfParticle).GetFields())
        {
            if (field.IsLiteral)
                continue;
            object? want = field.GetValue(expected);
            object? got = field.GetValue(actual);
            if (want is System.Collections.IEnumerable list && want is not string)
                Assert.That(got, Is.EqualTo(list), field.Name);
            else
                Assert.That(got, Is.EqualTo(want), field.Name);
        }

        Assert.That(read.Find("plain"), Is.Not.Null);
    }

    [Test]
    public void TwoEffectsWithOneNameAreRefused()
    {
        var library = new XpfLibrary { Particles = { new XpfParticle { Name = "a" }, new XpfParticle { Name = "a" } } };

        Assert.Throws<XpfFormatException>(() => XpfBinary.Write(library));
    }

    [Test]
    public void AScaleCurveOutOfOrderIsRefused()
    {
        XpfParticle particle = Sample();
        particle.ScaleCurve.Reverse();

        Assert.That(XpfValidation.Problem(particle), Is.Not.Null);
    }

    [Test]
    public void SomethingElseIsNotALibrary()
    {
        Assert.Throws<XpfFormatException>(() => XpfBinary.Read("XAF1 not particles"u8.ToArray()));
        Assert.Throws<XpfFormatException>(() => XpfBinary.Read(XpfBinary.Write(new XpfLibrary())[..20]));
    }

    [Test]
    public void TheShippedLibraryHoldsEveryEffectGameplaySetsOff()
    {
        XpfLibrary library = XpfBinary.Load(RepoFile(LibraryFile));

        foreach (string name in GameParticles.All)
            Assert.That(library.Find(name), Is.Not.Null, name);
    }

    private static string RepoFile(string relative)
    {
        for (string? dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(relative);
    }
}
