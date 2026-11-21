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

    [Test]
    public void ADeformGivesTheEffectItsOwnVertexStageWithTheSameValues()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test", """
            import xanadu;
            [Default(8)] uniform float height;
            uniform float4 tint;
            ItemVertex deform(ItemVertex v) { v.position.y += sin(time * 4 + v.uv.x * 6) * height; return v; }
            float4 shade(ItemInput i) { return i.colour * tint; }
            """);

        Assert.That(compiled.Vertex, Is.Not.Null);
        CompiledShader vertex = compiled.Vertex!;
        Assert.That(BitConverter.ToUInt32(vertex.Spirv, 0), Is.EqualTo(0x07230203u));
        // The values block is laid out as the fragment's, so one push fits both.
        Assert.That(vertex.Params.Select(p => (p.Name, p.Offset)), Is.EqualTo(compiled.Params.Select(p => (p.Name, p.Offset))));
        Assert.That(vertex.ParamsSize, Is.EqualTo(compiled.ParamsSize));
        // View, values, frame; nothing in the fragment's sets.
        Assert.That(vertex.UniformBuffers, Is.EqualTo((uint)SlangShaders.VertexFrameSlot + 1));
        Assert.That(SpirvScan.BindsSet(vertex.Spirv, 2) || SpirvScan.BindsSet(vertex.Spirv, 3), Is.False);
        Assert.That(vertex.StorageBuffers, Is.EqualTo(0));
    }

    [Test]
    public void ABodyWithoutDeformKeepsTheStandardVertexStage() =>
        Assert.That(SlangShaders.CompileEffectBody("test", "float4 shade(ItemInput i) { return i.colour; }").Vertex, Is.Null);

    [Test]
    public void ADeformThatReadsTheChartBindsItInTheVertexStage()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test", """
            import xanadu;
            ItemVertex deform(ItemVertex v) { v.position.x += chart[0].place.x; return v; }
            float4 shade(ItemInput i) { return i.colour; }
            """);

        Assert.That(compiled.Vertex!.StorageBuffers, Is.EqualTo(1));
    }

    [Test]
    public void ADeformThatSamplesATextureSaysOnlyShadeCan()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("test", """
            import xanadu;
            ItemVertex deform(ItemVertex v) { v.position.y += screenTexture.SampleLevel(v.uv, 0).r; return v; }
            float4 shade(ItemInput i) { return i.colour; }
            """));

        Assert.That(error!.Message, Does.Contain("deform can't read textures"));
    }

    [Test]
    public void ADeformingEffectDrawsShapesBentOrAtRest() =>
        Assert.That(SlangShaders.CompileEffectBody("test", """
            import xanadu;
            ItemVertex deform(ItemVertex v) { v.position += random(v.local) * 22 * sin(time); return v; }
            float4 shade(ItemInput i, Canvas canvas)
            {
                let bent = Circle(100).measured();
                let straight = Circle(100).measured(i.rest.centred);
                return colours.white.painted(bent.outlined(2), colours.red).painted(straight.outlined(2) * i.rest.aspect, colours.gold);
            }
            """).Vertex, Is.Not.Null);

    [Test]
    public void AKeptStateEffectCanDeform() =>
        Assert.That(SlangShaders.CompileEffectBody("test", """
            struct State { float x; };
            void update(inout State s, float dt) { s.x += dt; }
            ItemVertex deform(ItemVertex v) { v.position.x += time; return v; }
            float4 shade(ItemInput i) { return i.colour * state.x; }
            """, keepsState: true).Vertex, Is.Not.Null);

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

    [TestCase("colour c = colours.transparent; let ring = Circle(100).hollowed(10).measured(); let dot = Circle(30).moved(float2(100, 0)).measured(); c.paint(ring, colours.red); c.paint(dot.and(ring), colours.green); return c;")]
    [TestCase("let m = Circle(40).measured(); colour c = colours.black; c.paint(m.minus(Box(float2(20), 4)).melted(Star6(30), 8), Look(colours.gold, colours.white, 2)); c.erase(m.hollowed(2)); return c.painted(m.stroked(3) + m.haloed(40) + m.feathered(4), colours.red);")]
    [TestCase("let m = Circle(40).measured(canvas.centred - float2(10, 0)); return m.shown() * m.shadowed(20) * m.contoured(10) + m.or(m).meltedAnd(m, 3).meltedMinus(Circle(5), 2).filled();")]
    [TestCase("let m = Star6(60).melted(Circle(40), 20).measured(); return colours.white.painted(m.filled() + m.grown(10).stroked(2) + m.shrunk(10).or(m.grown(20)).stroked(2), colours.red);")]
    [TestCase("let hole = Circle(20).measured(); colour c = colours.black; c.paint(Star6(60).minus(hole).melted(Box(float2(30)), 4), Look(colours.gold, colours.white, 2)); c.erase(Circle(5).or(hole.grown(2))); return c.painted(Box(float2(80)).meltedAnd(hole, 3).meltedMinus(hole, 2), colours.red) * Circle(50).shadowed(20) * Circle(50).contoured(10) + Circle(5).and(hole).shown();")]
    [TestCase("let a = Circle(40).measured(); let b = Box(float2(30)).measured(); return colours.white.painted(a.or(b).and(a.measured()).minus(b.measured(canvas.centred)), colours.red) * a.distance();")]
    public void MeasuredShapesDrawAndJoinLikeShapes(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [Test]
    public void APointGivesTheAngleToAnother()
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { let eye = Box(float2(60, 12), 6).moved(float2(-80, 0)); float2 target = float2(40, 30); return colours.white.painted(eye.rotated(eye.placement.offset.angleTo(target)), colours.red) * float2(0).angleTo(canvas.centred); }"));
    }

    [TestCase("colour c = colours.gold; c.invert(); c.grey(); c.brighten(1.2); c.saturateBy(1.5); c.contrast(1.1); c.tint(colours.navy, 0.3); c.shiftHue(0.25); return c;")]
    [TestCase("return colours.gold.inverted().greyed().brightened(0.8).saturatedBy(0.5).contrasted(2).tinted(colours.red, 0.5).hueShifted(0.5).withRgb(float3(1, 0, 0));")]
    [TestCase("float l = colours.gold.luminance(); return l;")]
    [TestCase("colour a = colour(1.0, 0.3, 1.0); colour b = colour(a.rgb * 0.5); colour c = colour(0.2, 0.4, 0.6, 0.5); return a.over(b).over(c);")]
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

    [TestCase("return Circle(20).feathered(8) + sdf.feathered(Box(float2(30)).distance(), 4);")]
    [TestCase("Tile t = pattern.tile(40, anchor.topLeft); Tile u = pattern.tile(canvas.centred, 40, anchor.top); return Circle(10).filled(t.local) + Circle(5).filled(u.local);")]
    [TestCase("Tile t = pattern.tile(40, anchor.topLeft); return length(t.centre) / 40 + saw(time, 4) + triangle(time);")]
    [TestCase("Slice s = pattern.slice(6); Slice f = pattern.slice(canvas.centred, 5, float2(1, 0)); float2 back = rotate(s.local, s.turn); return Circle(10).filled(s.local + float2(0, -60)) + f.index + back.x;")]
    [TestCase("Tile t = pattern.tile(float2(80, 40)); Tile u = pattern.tile(canvas.centred, float2(60, 30), anchor.topLeft); return Box(float2(20)).filled(t.local) + length(u.centre);")]
    [TestCase("colour c = colours.rainbow(time, 1, 0.25); c.gammaCorrect(); return c.tinted(colours.palette(time, float3(0.5), float3(0.5), float3(1), float3(0, 0.33, 0.67)), 0.5).gammaCorrected(2.4);")]
    [TestCase("float v = sin01(1.0) + cos01(2.0) + remapped(0.5, float2(0, 1), float2(0.25, 0.75)); v.contrast(0.7); return v.contrasted(1.2);")]
    public void TheGapsFromPortingAreFilled(string body)
    {
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("test",
            "import xanadu;\nfloat4 shade(Canvas canvas) { " + body + " }"));
    }

    [Test]
    public void ABoolUniformIsASwitch()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test",
            "import xanadu;\n[Default(1)] uniform bool glow;\nuniform bool other;\nfloat4 shade(Canvas canvas) { return glow && !other ? 1 : 0; }");

        ShaderParam glow = compiled.Params.Single(p => p.Name == "glow");
        Assert.That(glow.IsBool, Is.True);
        Assert.That(glow.Default.X, Is.EqualTo(1));
        Assert.That(compiled.Params.Single(p => p.Name == "other").Default.X, Is.EqualTo(0));
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
    public void MatricesAndVectorsAreReflectedWithTheirShape()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("test", """
            import xanadu;
            uniform float2x3 wide;
            uniform float3x3 turn;
            uniform uint2 cells;
            uniform bool3 masks;
            float4 shade() { return float4(wide[1].z + turn[2].x + cells.y + (masks.z ? 1 : 0)); }
            """);

        ShaderParam wide = compiled.Params.Single(p => p.Name == "wide");
        ShaderParam turn = compiled.Params.Single(p => p.Name == "turn");
        ShaderParam cells = compiled.Params.Single(p => p.Name == "cells");
        ShaderParam masks = compiled.Params.Single(p => p.Name == "masks");
        Assert.That((wide.Rows, wide.Columns, wide.IsMatrix), Is.EqualTo((2, 3, true)));
        // Row by row, 16 bytes apart: two rows of a float2x3 take 32 bytes.
        Assert.That(wide.Size, Is.EqualTo(32));
        Assert.That((turn.Rows, turn.Columns), Is.EqualTo((3, 3)));
        Assert.That((cells.Columns, cells.IsInt, cells.IsUnsigned, cells.IsMatrix), Is.EqualTo((2, true, true, false)));
        Assert.That((masks.Columns, masks.IsBool), Is.EqualTo((3, true)));
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
