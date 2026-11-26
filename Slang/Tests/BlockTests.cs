using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Blocks: a library struct that owns its lists and passes, used by declaring it (<c>[Size(w, h)] Field field;</c>).</summary>
[TestFixture]
public sealed class BlockTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("block", ShaderStack.Compose("block", [new StackFile("graph.slang", source)]));

    private const string Graph = """
        import xanadu;

        [Default(2)] uniform float fade;
        [Size(64, 32)] Field field;
        [Size(8, 8)] Field small;

        [Pass(64, over: 64)]
        void stir(uint i, float amount)
        {
            field.deposit(float2(i / 64.0, 0.5), amount * field.at(float2(0.5, 0.5)));
        }

        void graph()
        {
            stir(0.25);
            field.spread(fade, 0.4);
            small.spread(1, 0.5);
        }
        """;

    [Test]
    public void ADeclaredBlockLaysOutItsListsUnderItsName()
    {
        StackBuild build = Compile(Graph).Build!.Shape;
        List<BuildBuffer> buffers = [.. build.Buffers];

        Assert.That(buffers.Select(buffer => buffer.Name), Is.SupersetOf(new[] { "field_cells", "field_before", "field_deposits", "small_cells" }));
        Assert.That(buffers.Single(buffer => buffer.Name == "field_cells").Count, Is.EqualTo(64 * 32));
        Assert.That(buffers.Single(buffer => buffer.Name == "small_deposits").Count, Is.EqualTo(64));
        Assert.That(buffers.Single(buffer => buffer.Name == "field_before").Source, Is.EqualTo(buffers.FindIndex(buffer => buffer.Name == "field_cells")));
    }

    [Test]
    public void ABlocksPassRunsOverItsOwnSizeOncePerDeclaration()
    {
        StackBuild build = Compile(Graph).Build!.Shape;
        var runs = new List<StackBuild.Run>();
        build.Walk(name => name == "fade" ? new BuildValue(2, false) : null, runs);

        Assert.That(runs.Select(run => build.Passes[run.Pass].Called), Is.EqualTo(new[] { "stir", "field.spread", "small.spread" }));
        Assert.That(runs[1].Over, Is.EqualTo(64u * 32));
        Assert.That(runs[2].Over, Is.EqualTo(64u));
    }

    [Test]
    public void FloatArgumentsGoAsTheirBits()
    {
        StackBuild build = Compile(Graph).Build!.Shape;
        var runs = new List<StackBuild.Run>();
        build.Walk(name => name == "fade" ? new BuildValue(2, false) : null, runs);

        Assert.That(System.BitConverter.UInt32BitsToSingle(runs[0].Values[0]), Is.EqualTo(0.25f));
        Assert.That(System.BitConverter.UInt32BitsToSingle(runs[1].Values[0]), Is.EqualTo(2f));
        Assert.That(System.BitConverter.UInt32BitsToSingle(runs[1].Values[1]), Is.EqualTo(0.4f));
    }

    [Test]
    public void APassTheBlockDoesntHaveIsNamed()
    {
        string wrong = Graph.Replace("field.spread(fade, 0.4);", "field.blur(fade);");
        Assert.That(() => Compile(wrong), Throws.Exception.With.Message.Contains("no pass blur").And.Message.Contains("spread"));
    }

    [Test]
    public void ABlockIsSizedAsItSays()
    {
        string wrong = Graph.Replace("[Size(64, 32)] Field field;", "[Size(64)] Field field;");
        Assert.That(() => Compile(wrong), Throws.Exception.With.Message.Contains("sized by w and h"));
    }

    private const string Generic = """
        import xanadu;

        struct Hit { float2 at; uint by; uint pad; };

        [Size(128)] Events<Hit> hits;

        [Pass(1, over: 1)]
        void plan(uint i) { hits.turn(); }

        [Pass(64, over: 128)]
        void hit(uint i)
        {
            if (i < hits.heard && hits[i].by == 0)
                return;
            Hit h = { float2(i, 0), i, 0 };
            hits.add(h);
        }

        void graph() { plan(); hit(); }
        """;

    [Test]
    public void AGenericBlockHoldsTheDeclaredType()
    {
        StackBuild build = Compile(Generic).Build!.Shape;
        List<BuildBuffer> buffers = [.. build.Buffers];

        Assert.That(buffers.Single(buffer => buffer.Name == "hits_items").Type, Is.EqualTo("Hit"));
        Assert.That(buffers.Single(buffer => buffer.Name == "hits_last").Source, Is.EqualTo(buffers.FindIndex(buffer => buffer.Name == "hits_items")));
        Assert.That(buffers.Single(buffer => buffer.Name == "hits_tally").Stored, Is.True);
        Assert.That(build.Bundles.Single().Type, Is.EqualTo("Events<Hit>"));
    }

    [Test]
    public void AGenericBlockSaysWhatItHolds()
    {
        Assert.That(() => Compile(Generic.Replace("Events<Hit> hits;", "Events hits;")), Throws.Exception.With.Message.Contains("say what hits holds"));
    }

    private const string Pooled = """
        import xanadu;

        struct Spark { float2 at; float age; uint pad; };

        [Size(256)] Pool<Spark> sparks;
        [Size(32)] Slots spare;

        [Pass(1, over: 1)]
        void plan(uint i) { sparks.turn(); }

        [Pass(64, over: 256)]
        void step(uint i)
        {
            Spark s = sparks[i];
            s.age += dt;
            if (s.age > 1)
                sparks.give(i);
            else
                sparks.live(i);
            sparks.put(i, s);
            uint slot;
            if (sparks.take(slot))
                sparks.put(slot, s);
        }

        void graph() { plan(); step(); }
        """;

    [Test]
    public void ABlocksSlotsAndGatheredShareTheBookkeeping()
    {
        StackBuild build = Compile(Pooled).Build!.Shape;
        List<BuildBuffer> buffers = [.. build.Buffers];

        Assert.That(buffers.Select(buffer => buffer.Name), Is.EquivalentTo(new[] { "sparks_items", "bookkeeping" }));
        // The pool's free places (2 + 256) and live list (1 + 256), then the stack's own Slots (2 + 32).
        Assert.That(buffers.Single(buffer => buffer.Name == "bookkeeping").Count, Is.EqualTo(258 + 257 + 34));
        Assert.That(buffers.Single(buffer => buffer.Name == "sparks_items").Type, Is.EqualTo("Spark"));
    }
}
