using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// A BUILD brick calling a library's passes and schedules (<c>xanadu.sort</c>, <c>xanadu.bvh</c>):
/// each lending of buffers is a pass of its own, binding the buffers lent; a schedule runs as if
/// written in build(); coherent buffers stay coherent though Slang sees them plain.
/// </summary>
[TestFixture]
public sealed class StackLibraryTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private const string Tree = """
        import xanadu;

        static const uint MAX = 4096;

        uniform int many;

        [Buffer(MAX)] RWStructuredBuffer<uint> keys;
        [Buffer(MAX)] RWStructuredBuffer<uint> ids;
        [Buffer(MAX)] RWStructuredBuffer<uint> keysB;
        [Buffer(MAX)] RWStructuredBuffer<uint> idsB;
        [Buffer(MAX)] RWStructuredBuffer<uint> counts;
        [Buffer(2 * MAX)] globallycoherent RWStructuredBuffer<bvh.Node> nodes;
        [Buffer(2 * MAX)] RWStructuredBuffer<uint> parents;
        [Buffer(MAX)] globallycoherent RWStructuredBuffer<uint> arrivals;

        [Pass(256, over: 1024 << many)]
        void code(uint i)
        {
            keys[i] = bvh.morton(float3(i, i * 7 % 13, i * 3 % 5), Bounds(float3(0), float3(4096)));
            ids[i] = i;
        }

        [Pass(256, over: 1024 << many)]
        void leaves(uint i) { nodes[(1024u << many) - 1 + i] = bvh.leaf(Ball(float3(i, 0, 0), 1).box, i, ids[i]); }

        void build()
        {
            uint n = 1024 << many;
            code();
            sort.radix(n, keys, ids, keysB, idsB, counts);
            leaves();
            bvh.tree(n, keys, nodes, parents, arrivals);
        }

        """;

    private const string Shade = """
        import xanadu;

        float4 shade(ItemInput i) { return float4(nodes[0].low, 1); }

        """;

    private static string Body(string build) =>
        ShaderStack.Compose("s", [new Brick(BrickKind.Build, build), new Brick(BrickKind.Shade, Shade)]);

    private static CompiledBuild Compiled(string build = Tree) => SlangShaders.CompileEffectBody("s", Body(build), keepsState: true).Build!;

    private static int Buffer(CompiledBuild build, string name) => build.Shape.Buffers.ToList().FindIndex(buffer => buffer.Name == name);

    [Test]
    public void EachLendingOfALibraryPassIsAPassOfItsOwn()
    {
        CompiledBuild build = Compiled();

        Assert.That(build.Passes.Select(pass => pass.Name), Is.EqualTo((string[])
            ["code", "leaves", "sort.hist", "sort.scan", "sort.scatter", "sort.hist 2", "sort.scatter 2", "bvh.link", "bvh.refit"]));
    }

    [Test]
    public void ALibraryPassBindsTheBuffersItWasLent()
    {
        CompiledBuild build = Compiled();
        CompiledPass first = build.Passes.Single(pass => pass.Name == "sort.hist");
        CompiledPass second = build.Passes.Single(pass => pass.Name == "sort.hist 2");

        Assert.That(first.Buffers, Is.EqualTo((int[])[Buffer(build, "keys"), Buffer(build, "counts")]));
        Assert.That(second.Buffers, Is.EqualTo((int[])[Buffer(build, "keysB"), Buffer(build, "counts")]));
        Assert.That(first.Threads, Is.EqualTo(256));
    }

    [Test]
    public void ALibrarysScheduleRunsAsIfWrittenInBuild()
    {
        StackBuild shape = Compiled().Shape;
        var runs = new List<StackBuild.Run>();

        shape.Walk(name => name == "many" ? BuildValue.Of(2) : null, runs);

        string[] names = [.. runs.Select(run => shape.Passes[run.Pass].Called)];
        Assert.That(names, Is.EqualTo((string[])
        [
            "code",
            "sort.hist", "sort.scan", "sort.scatter", "sort.hist", "sort.scan", "sort.scatter",
            "sort.hist", "sort.scan", "sort.scatter", "sort.hist", "sort.scan", "sort.scatter",
            "leaves", "bvh.link", "bvh.refit",
        ]));
        // Its passes size themselves from the arguments they were given, never the brick's names.
        StackBuild.Run hist = runs[1];
        Assert.That((hist.Groups, hist.Values[0], hist.Values[1]), Is.EqualTo((16u, 4096u, 0u)));
        Assert.That(runs[4].Values[1], Is.EqualTo(1u));
        Assert.That(runs[2].Values[0], Is.EqualTo(256u * 16));
        Assert.That((runs[14].Over, runs[15].Over), Is.EqualTo((4095u, 4096u)));
    }

    [Test]
    public void CoherentBuffersStayCoherentWhenLentToALibrary()
    {
        CompiledBuild build = Compiled();
        CompiledPass refit = build.Passes.Single(pass => pass.Name == "bvh.refit");

        int nodes = System.Array.IndexOf(refit.Buffers, Buffer(build, "nodes"));
        int parents = System.Array.IndexOf(refit.Buffers, Buffer(build, "parents"));
        Assert.That(Coherent(refit.Shader.Spirv, 1, nodes), Is.True);
        Assert.That(Coherent(refit.Shader.Spirv, 1, parents), Is.False);
    }

    [Test]
    public void LendingABufferOfTheWrongTypeIsRefusedWhereItIs()
    {
        string wrong = Tree.Replace("bvh.tree(n, keys, nodes,", "bvh.tree(n, nodes, nodes,");
        var error = Assert.Throws<System.InvalidOperationException>(() => Compiled(wrong))!;

        Assert.That(error.Message, Does.StartWith("s/build.slang(32): error:").And.Contain("bvh.tree takes RWStructuredBuffer<uint> for keys"));
    }

    [Test]
    public void CallingWhatALibraryDoesntShipIsRefused()
    {
        string wrong = Tree.Replace("bvh.tree(", "bvh.trees(");
        var error = Assert.Throws<System.InvalidOperationException>(() => Compiled(wrong))!;

        Assert.That(error.Message, Does.Contain("the library bvh has no pass or schedule trees"));
    }

    [Test]
    public void AModulesPassesAndSchedulesAreBlankedForSlangOnTheirLines()
    {
        const string module = "module m;\n[Pass(64, over: n)]\npublic void p(uint i, uint n) {}\n[Schedule]\nvoid s(uint n)\n{\n    p(n);\n}\nint after;\n";
        string prepared = StackLibrary.Prepared(module);

        Assert.That(prepared, Does.Not.Contain("[Pass").And.Not.Contain("[Schedule").And.Not.Contain("p(n)"));
        Assert.That(prepared.Split('\n').ToList().IndexOf("int after;"), Is.EqualTo(8));
        Assert.That(StackLibrary.Of("m", module)!.Schedules.Single().Name, Is.EqualTo("s"));
    }

    /// <summary>Whether the variable at this set and binding is decorated Coherent.</summary>
    private static bool Coherent(byte[] code, int set, int binding)
    {
        uint[] words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code).ToArray();
        var sets = new Dictionary<uint, uint>();
        var bindings = new Dictionary<uint, uint>();
        var coherent = new HashSet<uint>();
        for (int i = 5; i < words.Length; i += (int)(words[i] >> 16))
        {
            if ((words[i] & 0xFFFF) != 71)
                continue;
            if (words[i + 2] == 23)
                coherent.Add(words[i + 1]);
            else if (words[i + 2] == 34)
                sets[words[i + 1]] = words[i + 3];
            else if (words[i + 2] == 33)
                bindings[words[i + 1]] = words[i + 3];
        }

        return bindings.Any(pair => pair.Value == binding && sets.GetValueOrDefault(pair.Key) == set && coherent.Contains(pair.Key));
    }
}
