using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Every preset "+ FILE" offers compiles as it comes, beside a plain shade, with or without a graph already there.</summary>
[TestFixture]
public sealed class StackPresetsTests
{
    private const string PlainShade = "import xanadu;\nfloat4 shade(ItemInput i) { return i.colour; }\n";

    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static IEnumerable<string> Names() => StackPresets.All.Select(preset => preset.Name);

    private static StackPreset Named(string name) => StackPresets.All.Single(preset => preset.Name == name);

    [TestCaseSource(nameof(Names))]
    public void CompilesInAStackWithoutAGraph(string name)
    {
        StackPreset preset = Named(name);
        List<StackFile> files = [new("added.slang", preset.Text("added.slang", hasGraph: false))];
        if (preset.Role != StackRole.Shade)
            files.Add(new StackFile("shade.slang", PlainShade));

        Assert.That(SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", files)).Spirv.Length, Is.GreaterThan(20));
    }

    [TestCaseSource(nameof(Names))]
    public void CompilesBesideAGraphThatRunsIt(string name)
    {
        StackPreset preset = Named(name);
        if (preset.Role == StackRole.Graph)
            Assert.Ignore("A stack has one graph: this preset is refused beside another.");
        string calls = string.Join(" ", preset.InGraph.Select(call => call + ";"));
        List<StackFile> files =
        [
            new("added.slang", preset.Text("added.slang", hasGraph: true)),
            new("graph.slang", $"import xanadu;\n[Size(4)] List<float> other;\n[Pass(4, over: 4)] void tick(uint i) {{ other[i] = time; }}\nvoid graph() {{ tick(); {calls} }}\n"),
        ];
        if (preset.Role != StackRole.Shade)
            files.Add(new StackFile("shade.slang", PlainShade));

        Assert.That(SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", files)).Spirv.Length, Is.GreaterThan(20));
    }

    [Test]
    public void ARoleTheStackHasIsRefused()
    {
        Assert.That(Named("Shade").Refused([StackRole.Shade]), Does.Contain("shade()"));
        Assert.That(Named("Mesh").Refused([StackRole.Shade, StackRole.Graph]), Is.Null);
    }
}
