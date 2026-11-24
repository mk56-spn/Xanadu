using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>A stack's bricks compose into one body in pipeline order, refuse what can't work, and report problems on the brick that has them.</summary>
[TestFixture]
public sealed class ShaderStackTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private const string Fragment = "import xanadu;\nfloat4 shade(Canvas canvas) { return float4(1, 0, 0, 1); }\n";
    private const string Vertex = "import xanadu;\nItemVertex deform(ItemVertex v) { return v; }\n";

    [Test]
    public void BricksComposeInPipelineOrderUnderTheirOwnLabels()
    {
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Shade, Fragment), new Brick(BrickKind.Deform, Vertex)]);

        Assert.That(body.IndexOf("#line 1 \"s/deform.slang\"", StringComparison.Ordinal),
            Is.LessThan(body.IndexOf("#line 1 \"s/shade.slang\"", StringComparison.Ordinal)));
    }

    [Test]
    public void AStackWithoutAFragmentBrickKeepsTheItemsColour()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", [new Brick(BrickKind.Deform, Vertex)]));

        Assert.That(compiled.Vertex, Is.Not.Null);
    }

    [Test]
    public void ABrickWithoutItsEntryIsRefusedOnThatBrick()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderStack.Compose("s", [new Brick(BrickKind.Deform, "import xanadu;\n")]))!;

        Assert.That(error.Message, Does.StartWith("s/deform.slang(1): error:"));
    }

    [Test]
    public void AnEntryInTheWrongBrickSaysWhereItBelongs()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderStack.Compose("s", [new Brick(BrickKind.Shade, Fragment + Vertex)]))!;

        Assert.That(error.Message, Does.StartWith("s/shade.slang(4): error:").And.Contain("deform.slang"));
    }

    [Test]
    public void APoolNeedsAVertexBrickToPlaceItsItems()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ShaderStack.Compose("s", [new Brick(BrickKind.Step, ShaderStack.Template(BrickKind.Step)), new Brick(BrickKind.Shade, Fragment)]))!;

        Assert.That(error.Message, Does.StartWith("s/step.slang(1): error:"));
    }

    [Test]
    public void SlangsErrorsLandOnTheBrickThatHasThem()
    {
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Deform, Vertex), new Brick(BrickKind.Shade, "import xanadu;\n\nfloat4 shade(Canvas canvas) { return nope; }\n")]);
        Exception error = Assert.Catch(() => SlangShaders.CompileEffectBody("s", body))!;

        List<SlangProblem> onFragment = SlangProblems.Parse(error.Message, ShaderStack.Label("s", BrickKind.Shade));
        Assert.That(onFragment, Has.Count.GreaterThan(0));
        Assert.That(onFragment[0].Line, Is.EqualTo(3));
        Assert.That(SlangProblems.Parse(error.Message, ShaderStack.Label("s", BrickKind.Deform)).Where(problem => problem.Line > 0), Is.Empty);
    }

    [Test]
    public void APoolCompilesItsStepAndSizesItsItems()
    {
        string body = ShaderStack.Compose("s", [.. ShaderStack.Kinds.Where(kind => kind != BrickKind.Update).Select(kind => new Brick(kind, ShaderStack.Template(kind)))]);
        CompiledShader compiled = SlangShaders.CompileEffectBody("s", body, keepsState: true);

        Assert.That(compiled.Pool, Is.Not.Null);
        Assert.That(compiled.Pool!.ItemCount, Is.EqualTo(1000));
        // The item (two float2s) and whether it is alive, padded to the item's alignment.
        Assert.That(compiled.Pool.ItemBytes, Is.EqualTo(24));
        Assert.That(compiled.Pool.PoolRate, Is.EqualTo(0));
    }

    /// <summary>The pool template with its step made to die and a rate of births.</summary>
    private static string Born(string pool = "[Pool(1000, 500)]") =>
        ShaderStack.Template(BrickKind.Step)
            .Replace("void step(inout Item t, uint i, float dt)", "bool step(inout Item t, uint i, float dt)")
            .Replace("t.position += t.velocity * dt;", "t.position += t.velocity * dt;\n    return length(t.position) < 600;")
            .Replace("[Pool(1000)]", pool);

    [Test]
    public void APoolBornAtARateCompilesWithItsRate()
    {
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Step, Born()), new Brick(BrickKind.Deform, Vertex)]);
        CompiledShader compiled = SlangShaders.CompileEffectBody("s", body, keepsState: true);

        Assert.That(compiled.Pool!.PoolRate, Is.EqualTo(500));
    }

    [TestCase("[Pool(1000, 500)]", "void step")]
    [TestCase("[Pool(1000)]", "bool step")]
    [TestCase("[Pool(1000, fast)]", "bool step")]
    [TestCase("", "bool step")]
    public void BirthsAndDeathsGoTogether(string pool, string step)
    {
        string brick = Born(pool);
        if (step == "void step")
            brick = brick.Replace("bool step", "void step").Replace("\n    return length(t.position) < 600;", "");
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Step, brick), new Brick(BrickKind.Deform, Vertex)]);

        Exception error = Assert.Catch(() => SlangShaders.CompileEffectBody("s", body, keepsState: true))!;
        Assert.That(error.Message, Does.StartWith("s/step.slang("));
    }

    [Test]
    public void APoolOutsideTheLabIsRefused()
    {
        string body = ShaderStack.Compose("s", [.. ShaderStack.Kinds.Where(kind => kind != BrickKind.Update).Select(kind => new Brick(kind, ShaderStack.Template(kind)))]);

        Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", body));
    }

    [TestCaseSource(nameof(AllKinds))]
    public void EveryTemplateCompilesAsAStackOfItsOwn(BrickKind kind)
    {
        // A pool needs its deform brick; everything else stands alone.
        List<Brick> bricks = [new Brick(kind, ShaderStack.Template(kind))];
        if (kind == BrickKind.Step)
            bricks.Add(new Brick(BrickKind.Deform, ShaderStack.Template(BrickKind.Deform)));

        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", bricks), keepsState: true));
    }

    [Test]
    public void ATraceAloneComputesInItsTilesAndDrawsNoQuad()
    {
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Trace, ShaderStack.Template(BrickKind.Trace))]);

        CompiledTrace trace = SlangShaders.CompileEffectBody("s", body, keepsState: true).Trace!;

        Assert.That((trace.Width, trace.Height, trace.Alone), Is.EqualTo((8, 8, true)));
    }

    [Test]
    public void ATraceReadsTheBuildsBuffersAndKeepsItsQuadUnderAShade()
    {
        const string build = "import xanadu;\n[Buffer(64)] RWStructuredBuffer<float> heights;\n[Pass(64, over: 64)] void grow(uint i) { heights[i] = i; }\nvoid build() { grow(); }\n";
        const string trace = "import xanadu;\n[Tile(16, 4)] float4 trace(ItemInput i) { traced.depth = heights[group.index % 64]; return float4(1); }\n";
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Build, build), new Brick(BrickKind.Trace, trace), new Brick(BrickKind.Shade, Fragment)]);

        CompiledTrace compiled = SlangShaders.CompileEffectBody("s", body, keepsState: true).Trace!;

        Assert.That((compiled.Width, compiled.Height, compiled.Alone, compiled.Buffers), Is.EqualTo((16, 4, false, new[] { 0 })));
    }

    [Test]
    public void ATraceOutsideTheLabIsRefused()
    {
        string body = ShaderStack.Compose("s", [new Brick(BrickKind.Trace, ShaderStack.Template(BrickKind.Trace))]);

        Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", body));
    }

    private static BrickKind[] AllKinds() => ShaderStack.Kinds;
}
