using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// xanadu.anim compiles as it is written in Docs/shaders.md: clocks, clips and
/// their builders, beats, staggering and keys. Real Slang, real library; no GPU.
/// </summary>
[TestFixture]
public sealed class AnimLibraryTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static void Compiles(string body) =>
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", "import xanadu;\n" + body));

    [Test]
    public void AClipScalesAShape() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Clip pop = clock.looped(2).clip(0.2, 0.6).eased(eases.outBack);
            Circle dot = Circle(90);
            dot.scale(pop.progress);
            c.paint(dot, colours.gold);
            return c;
        }
        """);

    [Test]
    public void ClipsFollowOneAnother() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Clock loop = clock.looped(2.4);
            Clip slide = loop.clip(0, 0.5).eased(eases.outCubic);
            Clip spin  = slide.then(0.4).eased(eases.inOutSine);
            Clip leave = spin.then(0.3).delayed(0.6).eased(eases.inQuad);
            Box card = Box(float2(160, 90), 18);
            card.move(float2(slide.mixed(-300, 0), 0));
            card.rotate(spin.mixed(0, PI / 2));
            c.paint(card, colours.hotPink.withAlpha(1 - leave.progress));
            return c;
        }
        """);

    [Test]
    public void BeatsCountAndLoop() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Clip hit = clock.beats.looped(1).clip(0, 0.75).eased(eases.outExpo);
            bool downbeat = clock.beats.count % 4 == 0;
            float size = downbeat ? 1.4 : 1;
            Circle ring = Circle(hit.mixed(40, 140) * size);
            c.paint(ring.stroked(6), colours.deepSkyBlue.withAlpha(1 - hit.progress));
            c.paint(Circle(30 * size).scaled(hit.mixed(1.3, 1)), colours.white);
            return c;
        }
        """);

    [Test]
    public void ClipsStaggerPerTile() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Tile t = pattern.tile(36);
            float ring = length(t.id);
            Clip rise = clock.looped(2.4).clip(0, 0.45)
                .staggered(ring, 0.07)
                .bounced()
                .eased(eases.inOutSine);
            float dot = Circle(rise.mixed(2, 13)).filled(t.local);
            c.paint(dot, colours.gold.withAlpha(rise.mixed(0.3, 1)));
            return c;
        }
        """);

    [Test]
    public void KeysFollowAClip() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Clip trip = clock.looped(3).clip(0.2, 2.2).eased(eases.inOutSine);
            Keys<float2> path = Keys<float2>(float2(-180, 60))
                .to(0.5, float2(0, -90), eases.outQuad)
                .to(1, float2(180, 60), eases.inQuad);
            Keys<colour> tint = Keys<colour>(colours.gold)
                .to(0.5, colours.hotPink)
                .to(1, colours.deepSkyBlue);
            Circle dot = Circle(26);
            dot.move(path.at(trip));
            c.paint(dot, tint.at(trip));
            return c;
        }
        """);

    [Test]
    public void KeysCountFromTheirLastPress() => Compiles("""
        float4 shade(Canvas canvas)
        {
            colour c = colours.midnightBlue;
            Clip flash = clock.since(keys.action1).clip(0, 0.25).eased(eases.outQuad);
            c.paint(Circle(flash.mixed(120, 40)), colours.white.withAlpha(1 - flash.progress));
            if (keys.anyDirection.pressedWithin(0.1))
                c.paint(Box(float2(40)), colours.gold);
            float sinceLeft = keys.left.since;
            return c.painted(Circle(10).moved(float2(min(sinceLeft, 1) * 100, 0)), colours.hotPink);
        }
        """);

    [Test]
    public void AToolCanDriveTheClock()
    {
        string body = """
            import xanadu;
            uniform float2 xanaduClock;
            float4 shade(Canvas canvas) { return colours.gold * clock.looped(1).clip(0, 1).progress; }
            """;
        Assert.That(SlangShaders.DrivesClock(body), Is.True);
        Assert.That(SlangShaders.Wrap("test", body), Does.Contain("time = effect0::xanaduClock.x"));
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", body));
    }
}
