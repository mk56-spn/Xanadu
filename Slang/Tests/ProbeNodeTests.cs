using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Probe nodes and <c>[Reported]</c>: a probing body gets a pass per draw writing the shared probe list.</summary>
[TestFixture]
public sealed class ProbeNodeTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("probe", ShaderStack.Compose("probe", [new StackFile("graph.slang", source)]));

    private static string FlatMeshes() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Lab/flat_meshes/flat.slang"));

    [Test]
    public void ProbingAddsAPassPerDraw()
    {
        StackBuild build = Compile(FlatMeshes() + "\nuniform float4 xanaduProbe;\n").Build!.Shape;

        Assert.That(build.Passes.Select(pass => pass.Name), Does.Contain("xanaduProbe_shown"));
        Assert.That(build.Buffers.Single(buffer => buffer.Name == ProbeNodes.List).Shared, Is.True);
        Assert.That(build.Buffers.Single(buffer => buffer.Name == ProbeNodes.Count).Stored, Is.True);
    }

    [Test]
    public void NoWindowNoProbes()
    {
        StackBuild build = Compile(FlatMeshes()).Build!.Shape;

        Assert.That(build.Passes.Select(pass => pass.Name), Does.Not.Contain("xanaduProbe_shown"));
    }

    [Test]
    public void TheLanesStackCompiles()
    {
        string folder = Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Editor/probe_lanes");
        StackBuild build = SlangShaders.CompileEffectBody("lanes", ShaderStack.Compose(folder, ShaderStack.Read(folder))).Build!.Shape;

        Assert.That(build.Buffers.Single(buffer => buffer.Name == ProbeNodes.List).Count, Is.EqualTo(ProbeNodes.Lanes * ProbeNodes.Samples));
        Assert.That(build.Buffers.Where(buffer => buffer.Reported).Select(buffer => buffer.Name), Is.EquivalentTo(new[] { "rows" }));
    }

    [Test]
    public void ReportedListsAreMarked()
    {
        StackBuild build = Compile("""
            import xanadu;
            [Reported] Stored<uint> rows;
            [Pass(1, over: 1)] void count(uint i) { rows = 3; }
            void graph() { count(); }
            """).Build!.Shape;

        Assert.That(build.Buffers.Single(buffer => buffer.Name == "rows").Reported, Is.True);
    }
}
