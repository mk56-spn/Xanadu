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
    public void APoolStepsThenEachItemIsPlacedAndShaded()
    {
        StackGraph graph = Graph("swarm");

        Assert.That(graph.Passes.Select(p => p.Name), Is.EqualTo(new[] { "step", "deform", "shade" }));
        Assert.That(Access(graph, "step", "items"), Is.EqualTo(GraphAccess.ReadWrite));
        Assert.That(Access(graph, "deform", "items"), Is.EqualTo(GraphAccess.Read));
        Assert.That(Access(graph, "shade", "frame"), Is.EqualTo(GraphAccess.Write));
        Assert.That(graph.Resources.Single(r => r.Kind == GraphResourceKind.Pool).Bytes, Is.GreaterThan(0));
    }

    [Test]
    public void BuildPassesComeBeforeTheTraceThatReadsTheirBuffers()
    {
        StackGraph graph = Graph("bvh_march");
        string[] passes = [.. graph.Passes.Select(p => p.Name)];

        Assert.That(graph.Passes.TakeWhile(p => p.Brick == BrickKind.Build).Count(), Is.GreaterThan(3));
        Assert.That(passes.ToList().IndexOf("trace"), Is.GreaterThan(passes.ToList().FindLastIndex(n => graph.Passes.First(p => p.Name == n).Brick == BrickKind.Build)));
        // Bound read-write, as compute is; its code only reads.
        Assert.That(Access(graph, "trace", "nodes"), Is.EqualTo(GraphAccess.Read));
        Assert.That(Access(graph, "countSparks", "drawn"), Is.EqualTo(GraphAccess.Write));
        Assert.That(Access(graph, "trace", "depth")!.Value.HasFlag(GraphAccess.Write));
        // Its sparks are [Translucent]: they test the depth and never write it.
        Assert.That(Access(graph, "deform", "depth"), Is.EqualTo(GraphAccess.Read));
    }

    [Test]
    public void ThePassesAreTheOnesBuildCallsWithTheValuesAsTheyAre()
    {
        CompiledShader compiled = Compiled("bvh_march");
        StackGraph lit = StackGraph.Of(compiled);
        StackGraph dark = StackGraph.Of(compiled, name => compiled.ScheduleValue(name, name == "shadows" ? new Vector4(0) : null));

        Assert.That(lit.Passes.First().Name, Is.EqualTo("countSparks"));
        Assert.That(lit.Passes.Single(p => p.Name == "hist").Calls, Is.EqualTo(4));
        Assert.That(lit.Passes.Select(p => p.Name), Does.Contain("lightFill"));
        Assert.That(dark.Passes.Select(p => p.Name), Does.Not.Contain("lightFill"));
    }

    [Test]
    public void EveryBufferAPassTouchesIsAResourceOfTheStack()
    {
        StackGraph graph = Graph("bvh_march");

        Assert.That(graph.Edges.All(e => e.Pass < graph.Passes.Count && e.Resource < graph.Resources.Count));
        Assert.That(graph.Resources.Where(r => r.Kind == GraphResourceKind.Buffer).Select(r => r.Name), Does.Contain("drawn"));
    }
}
