using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// A BUILD brick: its buffers are sized from its constants, its passes compile one entry each
/// and bind only the buffers they touch, and its schedule runs on the CPU in the order written.
/// </summary>
[TestFixture]
public sealed class StackBuildTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private const string Sort = """
        import xanadu;

        static const uint N = 1024;
        static const uint BLOCK = 256;

        [Buffer(2 * N)] RWStructuredBuffer<uint> keys;
        [Buffer(N / BLOCK * 256)] RWStructuredBuffer<uint> counts;
        [Buffer(N)] globallycoherent RWStructuredBuffer<float4> balls;

        uniform bool spin;

        groupshared uint shared[BLOCK];

        [Pass(256, over: N)]
        void seed(uint i) { keys[i] = N - i; }

        [Pass(BLOCK, groups: N / BLOCK)]
        void count(uint i, uint round)
        {
            shared[dispatch.thread] = (keys[i] >> (round * 8)) & 0xFF;
            GroupMemoryBarrierWithGroupSync();
            counts[dispatch.group * 256 + dispatch.thread] = shared[dispatch.thread];
        }

        [Pass(64, over: N)]
        void turn(uint i) { balls[i] = float4(i, 0, 0, time); }

        void build()
        {
            seed();
            for (uint round = 0; round < 4; round++)
                count(round);
            if (spin)
                turn();
        }

        """;

    private const string Shade = """
        import xanadu;

        float4 shade(ItemInput i) { return float4(balls[0].xyz, 1); }

        """;

    private static string Body(string build, string shade = Shade) =>
        ShaderStack.Compose("s", [new StackFile("build.slang", build), new StackFile("shade.slang", shade)]);

    [Test]
    public void BuffersAreSizedFromTheBricksConstants()
    {
        CompiledBuild build = SlangShaders.CompileEffectBody("s", Body(Sort), keepsState: true).Build!;

        Assert.That(build.Shape.Buffers, Has.Count.EqualTo(3));
        Assert.That(build.Bytes, Is.EqualTo((long[])[2 * 1024 * 4, 1024 * 4, 1024 * 16]));
    }

    [Test]
    public void EachPassBindsOnlyTheBuffersItTouches()
    {
        CompiledBuild build = SlangShaders.CompileEffectBody("s", Body(Sort), keepsState: true).Build!;

        Assert.That(build.Passes[0].Buffers, Is.EqualTo((int[])[0]));
        Assert.That(build.Passes[1].Buffers, Is.EqualTo((int[])[0, 1]));
        Assert.That(build.Passes[2].Buffers, Is.EqualTo((int[])[2]));
        Assert.That(build.Passes[1].Threads, Is.EqualTo(256));
    }

    [Test]
    public void ShadeBindsTheBuffersItReadsAfterTheGlobals()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("s", Body(Sort), keepsState: true);

        Assert.That(compiled.StackBuffers, Is.EqualTo((int[])[2]));
        Assert.That(compiled.StorageBuffers, Is.EqualTo(SlangShaders.Globals.Count + 1));
    }

    [Test]
    public void TheScheduleRunsInTheOrderWrittenWithItsLoopsAndIfs()
    {
        StackBuild build = SlangShaders.CompileEffectBody("s", Body(Sort), keepsState: true).Build!.Shape;
        var on = new List<StackBuild.Run>();
        var off = new List<StackBuild.Run>();

        build.Walk(name => name == "spin" ? BuildValue.Of(true) : null, on);
        build.Walk(name => name == "spin" ? BuildValue.Of(false) : null, off);

        Assert.That(on.Select(run => run.Pass), Is.EqualTo((int[])[0, 1, 1, 1, 1, 2]));
        Assert.That(on.Where(run => run.Pass == 1).Select(run => run.Values[0]), Is.EqualTo((uint[])[0, 1, 2, 3]));
        Assert.That(off, Has.Count.EqualTo(5));
        // over: N is a thread each, rounded up to whole groups; groups: is taken as written.
        Assert.That(on[0].Groups, Is.EqualTo(4u));
        Assert.That(on[0].Over, Is.EqualTo(1024u));
        Assert.That(on[1].Groups, Is.EqualTo(4u));
        Assert.That(on[5].Groups, Is.EqualTo(16u));
    }

    [Test]
    public void ACallWithTheWrongArgumentsIsRefusedWhereItIs()
    {
        string wrong = Sort.Replace("count(round);", "count();");
        var error = Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", Body(wrong), keepsState: true))!;

        Assert.That(error.Message, Does.StartWith("s/build.slang(32): error:").And.Contain("count takes 1 argument"));
    }

    [Test]
    public void CallingWhatIsntAPassIsRefused()
    {
        string wrong = Sort.Replace("seed();", "seeds();");
        var error = Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", Body(wrong), keepsState: true))!;

        Assert.That(error.Message, Does.StartWith("s/build.slang(30): error:").And.Contain("seeds isn't a pass"));
    }

    [Test]
    public void SlangsErrorsInAPassLandOnItsOwnLine()
    {
        string wrong = Sort.Replace("keys[i] = N - i;", "keys[i] = nope;");
        Exception error = Assert.Catch(() => SlangShaders.CompileEffectBody("s", Body(wrong), keepsState: true))!;

        List<SlangProblem> problems = SlangProblems.Parse(error.Message, ShaderStack.Label("s", "build.slang"));
        Assert.That(problems, Has.Count.GreaterThan(0));
        Assert.That(problems[0].Line, Is.EqualTo(15));
    }

    [Test]
    public void ABufferWithoutASizeTheCpuKnowsIsRefused()
    {
        string wrong = Sort.Replace("[Buffer(2 * N)]", "[Buffer(2 * M)]");
        var error = Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", Body(wrong), keepsState: true))!;

        Assert.That(error.Message, Does.StartWith("s/build.slang(6): error:").And.Contain("'M'"));
    }

    [Test]
    public void ABuildOutsideTheLabIsRefused() =>
        Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", Body(Sort)));

    [Test]
    public void ADrawsBufferIsOneUintAndSaysWhichBufferItIs()
    {
        string draws = Sort.Replace("uniform bool spin;", "[Draws] RWStructuredBuffer<uint> drawn;\nuniform bool spin;")
            .Replace("void seed(uint i) { keys[i] = N - i; }", "void seed(uint i) { keys[i] = N - i; drawn[0] = N; }");
        CompiledBuild build = SlangShaders.CompileEffectBody("s", Body(draws), keepsState: true).Build!;

        Assert.That(build.Shape.Draws, Is.EqualTo(3));
        Assert.That(build.Bytes[3], Is.EqualTo(4));
        Assert.That(build.Passes[0].Buffers, Is.EqualTo((int[])[0, 3]));
    }

    [Test]
    public void ADrawsBufferOfAnythingButUintIsRefused()
    {
        string wrong = Sort.Replace("uniform bool spin;", "[Draws] RWStructuredBuffer<float> drawn;\nuniform bool spin;");
        var error = Assert.Throws<InvalidOperationException>(() => SlangShaders.CompileEffectBody("s", Body(wrong), keepsState: true))!;

        Assert.That(error.Message, Does.Contain("[Draws] buffer is one uint"));
    }

    [Test]
    public void WithoutADrawsBufferTheQuadIsDrawnOnce() =>
        Assert.That(SlangShaders.CompileEffectBody("s", Body(Sort), keepsState: true).Build!.Shape.Draws, Is.EqualTo(-1));
}
