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
    public void AShadeOnlyStackGetsAGraphRunningTheClipProbe()
    {
        string folder = Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Game/hit_bar");
        string source = ShaderStack.Compose(folder, ShaderStack.Read(folder)) + "\nuniform float4 xanaduProbe;\n";
        StackBuild build = SlangShaders.CompileEffectBody("hit_bar", source).Build!.Shape;

        Assert.That(build.Passes.Select(pass => pass.Name), Is.EqualTo(new[] { "xanaduProbe_clips" }));
        Assert.That(build.Buffers.Single(buffer => buffer.Name == ClipProbes.List).Shared, Is.True);
    }

    [Test]
    public void ClipsAreTaggedWithTheirSites()
    {
        const string code = "Clip a = clock.clip(0, 1);\n  Clip b = a.then(2).eased(eases.outQuad); // Clip c = no;\n";

        Assert.That(ClipProbes.Sites(code), Is.EqualTo(new[] { new ClipSite("a", 0, 5), new ClipSite("b", 1, 7) }));
        Assert.That(ClipProbes.Sited(code), Does.StartWith("Clip a =( clock.clip(0, 1)).xanaduSited(0);\n  Clip b =( a.then(2).eased(eases.outQuad)).xanaduSited(1);"));
    }

    [Test]
    public void AReportReadsAsClipsWithStaggerAndKeys()
    {
        var report = new System.Numerics.Vector4[ClipProbes.Threads * ClipProbes.Region];
        // Two threads (copies) saw clip site 0 starting 0.1 apart; the first saw a key at 0.5 on it.
        for (int t = 0; t < 2; t++)
        {
            int at = t * ClipProbes.Region;
            report[at] = new(1, t == 0 ? 1 : 0, 0, 0);
            report[at + 1] = new(0.2f + 0.1f * t, 0.6f, 2, 26);
            report[at + 2] = new(1 + 2, 1000, 0, 0);
            report[at + 33] = new(0, 0.5f, 0, 0);
        }

        SlangClip clip = ClipProbes.Read(System.Runtime.InteropServices.MemoryMarshal.AsBytes(report.AsSpan()), [new ClipSite("pop", 3, 9)]).Single();

        Assert.That((clip.Name, clip.Line, clip.Beats, clip.Bounced, clip.Loop, clip.Length, clip.Easing, clip.Pressed),
            Is.EqualTo(("pop", 3, true, true, 2.0, 0.6f, 26, 1000)));
        Assert.That(clip.Start, Is.EqualTo(0.2).Within(1e-6));
        Assert.That(clip.StaggerStep, Is.EqualTo(0.1).Within(1e-6));
        Assert.That(clip.Keys, Is.EqualTo(new[] { 0.5 }));
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
        Assert.That(build.Buffers.Where(buffer => buffer.Reported).Select(buffer => buffer.Name), Is.EquivalentTo(new[] { "rows", ClipProbes.List }));
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
