using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>A compiled stack read as a render graph: its passes in run order, and who reads and writes what.</summary>
[TestFixture]
public sealed class StackGraphTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compiled(string lab) =>
        ShippedEffectsTests.Compile(Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Lab", lab));

    private static StackGraph Graph(string lab) => StackGraph.Of(Compiled(lab));

    private static GraphAccess? Access(StackGraph graph, string pass, string resource)
    {
        int p = graph.Passes.ToList().FindIndex(x => x.Name == pass);
        int r = graph.Resources.ToList().FindIndex(x => x.Name == resource);
        GraphEdge[] edges = [.. graph.Edges.Where(e => e.Pass == p && e.Resource == r)];
        return edges.Length == 0 ? null : edges.Aggregate((GraphAccess)0, (all, e) => all | e.Access);
    }

    [Test]
    public void APassTouchingOnlyCachedImagesFillsTheCacheAndTheOneReadingItRunsEveryFrame()
    {
        CompiledShader compiled = Compiled("cached");
        GraphNode[] nodes = [.. GraphPlan.Of(compiled).Nodes.Where(n => n.Kind == NodeKind.Pixels)];
        string[] names = [.. nodes.Select(n => compiled.Build!.Passes[n.Call.Pass].Name)];

        Assert.That(names, Is.EqualTo(new[] { "carve", "light" }));
        Assert.That(nodes.Select(n => n.Fills), Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void APresentIsADrawOverTheBoxOntoTheFrame()
    {
        CompiledShader compiled = Compiled("sizes");
        CompiledPass[] presents = [.. compiled.Build!.Passes.Where(p => p.Name.StartsWith("present_", StringComparison.Ordinal))];
        GraphNode[] exits = [.. GraphPlan.Of(compiled).Nodes.Where(n => n.Place == NodePlace.Exit && n.Kind == NodeKind.Draw)];

        // One draw per pair of images presented, each covering the box; the default values present `shown`.
        Assert.That(presents.Select(p => p.Name), Is.EquivalentTo(new[] { "present_glow", "present_shown" }));
        Assert.That(presents.All(p => p.Drawn!.CoversBox), Is.True);
        Assert.That(exits.Select(n => compiled.Build.Passes[n.Call.Pass].Name), Does.Contain("present_shown"));
    }

    [Test]
    public void APoolIsAPassOverAKeptBufferThenACountedDraw()
    {
        StackGraph graph = Graph("swarm");

        Assert.That(graph.Passes.Select(p => p.Name), Is.EqualTo(new[] { "move", "ready", "dot" }));
        Assert.That(Access(graph, "move", "dots"), Is.EqualTo(GraphAccess.ReadWrite));
        Assert.That(Access(graph, "dot", "dots"), Is.EqualTo(GraphAccess.Read));
        Assert.That(Access(graph, "dot", "frame"), Is.EqualTo(GraphAccess.Write));
        Assert.That(graph.Resources.Single(r => r.Name == "dots").Bytes, Is.GreaterThan(0));
    }

    [Test]
    public void TheGraphsPassesComeBeforeTheMarchThatReadsTheirBuffers()
    {
        StackGraph graph = Graph("bvh_march");
        string[] passes = [.. graph.Passes.Select(p => p.Name)];

        Assert.That(passes.ToList().IndexOf("march"), Is.GreaterThan(passes.ToList().IndexOf("lightFill")));
        // Bound read-write, as compute is; its code only reads.
        Assert.That(Access(graph, "march", "nodes"), Is.EqualTo(GraphAccess.Read));
        // The march writes its two images; the present, lowered to a draw, reads them and lands them, depth with it.
        Assert.That(Access(graph, "march", "marched"), Is.EqualTo(GraphAccess.ReadWrite));
        Assert.That(Access(graph, "present_marched_marchedDepth", "marchedDepth"), Is.EqualTo(GraphAccess.Read));
        Assert.That(Access(graph, "present_marched_marchedDepth", "depth")!.Value.HasFlag(GraphAccess.Write));
        // Its sparks are drawn onto the frame.
        Assert.That(Access(graph, "spark", "frame"), Is.EqualTo(GraphAccess.Write));
    }

    [Test]
    public void ThePassesAreTheOnesBuildCallsWithTheValuesAsTheyAre()
    {
        CompiledShader compiled = Compiled("bvh_march");
        StackGraph lit = StackGraph.Of(compiled);
        StackGraph dark = StackGraph.Of(compiled, name => compiled.ScheduleValue(name, name == "shadows" ? new Vector4(0) : null));

        Assert.That(lit.Passes.First().Name, Is.EqualTo("animate"));
        // The library's radix sort: a histogram pass over each half of the keys, two rounds each.
        Assert.That(lit.Passes.Where(p => p.Name.StartsWith("sort.hist")).Select(p => (p.Name, p.Calls)),
            Is.EqualTo(((string, int)[])[("sort.hist", 2), ("sort.hist 2", 2)]));
        Assert.That(lit.Passes.Select(p => p.Name), Does.Contain("lightFill"));
        Assert.That(dark.Passes.Select(p => p.Name), Does.Not.Contain("lightFill"));
    }

    [Test]
    public void EveryBufferAPassTouchesIsAResourceOfTheStack()
    {
        StackGraph graph = Graph("bvh_march");

        Assert.That(graph.Edges.All(e => e.Pass < graph.Passes.Count && e.Resource < graph.Resources.Count));
        Assert.That(graph.Resources.Where(r => r.Kind == GraphResourceKind.Buffer).Select(r => r.Name), Does.Contain("light"));
    }
}
