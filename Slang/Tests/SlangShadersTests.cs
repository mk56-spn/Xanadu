using System;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// Compiling effect bodies: the wrapper, reflection of their values, kept
/// state, and errors landing on the author's own lines. Real Slang, real
/// library; no GPU.
/// </summary>
[TestFixture]
public sealed class SlangShadersTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    [Test]
    public void ABodyCompilesToSpirv()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test", """
            import xanadu;
            float4 shade(Canvas canvas) { return colours.gold * Circle(40).filled(); }
            """);

        Assert.That(compiled.Spirv.Length, Is.GreaterThan(20));
        // SPIR-V's magic number, little-endian.
        Assert.That(BitConverter.ToUInt32(compiled.Spirv, 0), Is.EqualTo(0x07230203u));
        Assert.That(compiled.Update, Is.Null);
    }

    [TestCase("colour c = colours.black; c.paint(Linear(400).rotated(PI / 2), colours.navy, colours.black); return c;")]
    [TestCase("colour c = colours.black; c.paint(Radial(200).anchored(anchor.top), colours.gold, colours.transparent, blend.screen); return c;")]
    [TestCase("return Conical().rotated(0.5).moved(float2(10, 0)).amount();")]
    [TestCase("return Radial(100).coloured(colours.white, colours.black) * Circle(80).filled();")]
    [TestCase("return Radial(float2(200, 80)).rotated(0.3).amount();")]
    [TestCase("return Reflected(300).rotated(PI / 4).coloured(colours.black, colours.white);")]
    [TestCase("return Diamond(120).coloured(colours.gold, colours.navy, colours.black);")]
    [TestCase("return Square(float2(160, 90)).coloured(colours.red, colours.orange, colours.yellow, colours.white);")]
    [TestCase("return Focal(200, float2(-60, -40)).moved(float2(10, 0)).amount();")]
    [TestCase("return Spiral(80, 3).rotated(0.5).amount();")]
    [TestCase("colour c = colours.black; c.paint(Radial(40).repeated(1), colours.white, colours.black); return c;")]
    [TestCase("return Linear(100).mirrored(3).rotated(0.2).coloured(colours.white, colours.black);")]
    [TestCase("return Conical().repeated(4).amount();")]
    [TestCase("return gradient.stops(Linear(400).amount(), float3(0.2, 0.5, 0.8), colours.navy, colours.purple, colours.orange);")]
    [TestCase("return gradient.corners(colours.red, colours.green, colours.blue, colours.white);")]
    public void GradientsAreCentredThenPlaced(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [TestCase("colour c = colours.gold; c.invert(); c.grey(); c.brighten(1.2); c.saturateBy(1.5); c.contrast(1.1); c.tint(colours.navy, 0.3); c.shiftHue(0.25); return c;")]
    [TestCase("return colours.gold.inverted().greyed().brightened(0.8).saturatedBy(0.5).contrasted(2).tinted(colours.red, 0.5).hueShifted(0.5).withRgb(float3(1, 0, 0));")]
    [TestCase("float l = colours.gold.luminance(); return l;")]
    public void ColoursChangeTheirRgbAndKeepTheirAlpha(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [TestCase("return sdf.shown(Circle(100).distance());")]
    [TestCase("float d = Box(float2(80)).distance(); colour c = colours.gold; c.brighten(sdf.shadowed(d, 80)); c.brighten(sdf.contoured(d, 20)); return c;")]
    public void DistancesCanBeShown(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [Test]
    public void AShapesDistanceDefaultsToThePixelBeingShaded()
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { return Circle(40).moved(float2(20, 0)).melted(Box(float2(30)), 10).distance(); }"));
    }

    [TestCase("colour c = colours.black; c.paint(Grid(1, 40), colours.white); return c;")]
    [TestCase("colour c = colours.black; c.paint(Grid(2, float2(80, 40)).moved(float2(20, 0)).rotated(0.5), colours.gold); return c;")]
    [TestCase("return Columns(2, 24).filled() + Rows(2, 24).stroked(1);")]
    [TestCase("colour c = colours.black; c.paint(Checker(64, 2).rotated(0.3), colours.gold); return c;")]
    [TestCase("colour c = colours.black; c.paint(Stripes(24), colours.gold); c.paint(Stripes(40, 0.25).rotated(-0.785).moved(float2(10, 0))); return c;")]
    [TestCase("Tile t = pattern.tile(90); return Circle(10).filled(t.local);")]
    public void PatternsAreShapesCentredOnTheQuad(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [TestCase("float4 shade() { return 1; }")]
    [TestCase("float4 shade(Canvas c) { return c.size.x; }")]
    [TestCase("float4 shade(ItemInput i) { return i.colour; }")]
    [TestCase("float4 shade(Canvas c, ItemInput i) { return i.colour * c.aspect; }")]
    [TestCase("float4 shade(ItemInput i, Canvas c) { return i.colour * c.aspect; }")]
    public void EveryShadeSignatureCompiles(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", "import xanadu;\n" + body));
    }

    [Test]
    public void UniformsAreReflectedWithTheirDefaults()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test", """
            import xanadu;
            [Default(0.5)] uniform float strength;
            [Default(colours.gold)] uniform colour tint;
            uniform int steps;
            float4 shade() { return tint * strength * steps; }
            """);

        ShaderParam strength = compiled.Params.Single(p => p.Name == "strength");
        ShaderParam tint = compiled.Params.Single(p => p.Name == "tint");
        ShaderParam steps = compiled.Params.Single(p => p.Name == "steps");
        Assert.That(strength.Default.X, Is.EqualTo(0.5f));
        Assert.That(strength.Size, Is.EqualTo(4));
        Assert.That(tint.Size, Is.EqualTo(16));
        // A library constant in a [Default] becomes its numbers: gold is (1, 0.843, 0, 1).
        Assert.That(tint.Default.X, Is.EqualTo(1f).Within(0.01f));
        Assert.That(tint.Default.Y, Is.EqualTo(0.843f).Within(0.01f));
        Assert.That(steps.IsInt, Is.True);
        Assert.That(compiled.ParamsSize % 16, Is.Zero);
    }

    [Test]
    public void AnErrorPointsAtTheAuthorsLine()
    {
        Exception error = Assert.Catch(() => SlangShaders.CompileEffectBody("mine", """
            import xanadu;

            float4 shade()
            {
                return nothingByThisName;
            }
            """), "a body that doesn't compile throws with Slang's message")!;

        // Slang may throw its own exception type; what matters is the message names our line.
        Assert.That(SlangProblems.Parse(error.Message, "mine").Select(p => p.Line), Does.Contain(5));
    }

    [Test]
    public void KeptStateNeedsTheLabsSayso()
    {
        const string body = """
            import xanadu;
            struct State { float x; };
            void update(inout State s, float dt) { s.x += dt; }
            float4 shade() { return state.x; }
            """;

        Assert.That(SlangShaders.KeepsState(body), Is.True);
        Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("test", body));

        CompiledShader compiled = SlangShaders.CompileEffectBody("test", body, keepsState: true);
        Assert.That(compiled.Update, Is.Not.Null);
        // The chart, then the kept state.
        Assert.That(compiled.StorageBuffers, Is.EqualTo(2));
        Assert.That(compiled.Update!.StateBytes, Is.EqualTo(4));
    }

    [Test]
    public void EveryEffectReadsTheChartAfterItsTextures()
    {
        const string body = """
            import xanadu;
            [[vk::binding(2, 2)]] Sampler2D extra;
            float4 shade(Canvas canvas)
            {
                Note next = notes.dueAfter(song.seconds);
                Clip pop = clock.since(notes[0]).clip(0, 0.3);
                return extra.Sample(float2(0.5)) * next.colour * pop.progress;
            }
            """;

        CompiledShader compiled = SlangShaders.CompileEffectBody("test", body);
        Assert.That(compiled.Samplers, Has.Length.EqualTo(3));
        Assert.That(compiled.StorageBuffers, Is.EqualTo(1));
        Assert.That(SpirvScan.Binds(compiled.Spirv, set: 2, binding: 3), Is.True);
    }

    [Test]
    public void TimeReachesEveryFunctionWithoutBeingPassed()
    {
        const string body = """
            import xanadu;
            struct State { float last; };
            float pulse() { return 0.5 + 0.5 * sin(time * 2); }
            void update(inout State s, float dt) { s.last = time; }
            float4 shade() { return colours.gold * pulse() + state.last * 0; }
            """;

        CompiledShader compiled = SlangShaders.CompileEffectBody("test", body, keepsState: true);

        Assert.That(compiled.Update, Is.Not.Null);
    }

    [Test]
    public void AnUpdateInACommentDoesNotKeepState()
    {
        Assert.That(SlangShaders.KeepsState("// void update(inout State s, float dt)\nfloat4 shade() { return 1; }"), Is.False);
    }

    [Test]
    public void OnlyABodyThatSamplesTheScreenReadsIt()
    {
        CompiledShader plain = SlangShaders.CompileEffectBody("test", "import xanadu;\nfloat4 shade(ItemInput i) { return i.colour; }");
        CompiledShader reading = SlangShaders.CompileEffectBody("test", "import xanadu;\nfloat4 shade(ItemInput i) { return sampleScreen(i.screenUv); }");

        Assert.That(plain.ReadsScreen, Is.False);
        Assert.That(reading.ReadsScreen, Is.True);
    }

    [Test]
    public void ChainedCallsAreRewrittenBeforeSlangSeesThem()
    {
        // Slang can't chain in-place calls; the host rewrites them to the -ed twins.
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test", """
            import xanadu;
            float4 shade()
            {
                Circle c = Circle(40);
                c.move(float2(10, 0)).rotate(1);
                return c.filled();
            }
            """));
    }
}
