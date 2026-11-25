using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>A stack's files compose into one body, each under its own label; what each does comes from its entries, not its name.</summary>
[TestFixture]
public sealed class ShaderStackTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private const string Fragment = "import xanadu;\nfloat4 shade(Canvas canvas) { return float4(1, 0, 0, 1); }\n";
    private const string Vertex = "import xanadu;\nItemVertex deform(ItemVertex v) { return v; }\n";
    private const string Graph = "import xanadu;\n[Buffer(64)] RWStructuredBuffer<float> heights;\n[Pass(64, over: 64)] void grow(uint i) { heights[i] = i; }\nvoid graph() { grow(); }\n";

    [Test]
    public void FilesComposeInTheOrderTheirRolesRunUnderTheirOwnLabels()
    {
        string body = ShaderStack.Compose("s", [new StackFile("look.slang", Fragment), new StackFile("bend.slang", Vertex)]);

        Assert.That(body.IndexOf("#line 1 \"s/bend.slang\"", StringComparison.Ordinal),
            Is.LessThan(body.IndexOf("#line 1 \"s/look.slang\"", StringComparison.Ordinal)));
    }

    [Test]
    public void AStackWithoutAShadeKeepsTheItemsColour()
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", [new StackFile("bend.slang", Vertex)]));

        Assert.That(compiled.Vertex, Is.Not.Null);
    }

    [Test]
    public void AGraphInAnyFileWithoutAShadeDrawsOnlyWhatItDraws()
    {
        string body = ShaderStack.Compose("Lab/s", [new StackFile("heights.slang", Graph)]);

        CompiledShader compiled = SlangShaders.CompileEffectBody("Lab/s", body, runsGraphs: true);

        Assert.That(compiled.Build!.Alone, Is.True);
    }

    [Test]
    public void DeclarationsMaySitInAnotherFileThanTheGraph()
    {
        const string declared = "import xanadu;\n[Buffer(64)] RWStructuredBuffer<float> heights;\n[Pass(64, over: 64)] void grow(uint i) { heights[i] = i; }\n";
        const string run = "import xanadu;\nvoid graph() { grow(); }\n";
        string body = ShaderStack.Compose("Lab/s", [new StackFile("heights.slang", declared), new StackFile("run.slang", run), new StackFile("look.slang", Fragment)]);

        CompiledShader compiled = SlangShaders.CompileEffectBody("Lab/s", body, runsGraphs: true);

        Assert.That(compiled.Build!.Passes.Select(pass => pass.Name), Is.EqualTo(new[] { "grow" }));
    }

    [Test]
    public void AnEntryTwiceInTheStackIsRefusedOnTheSecondFile()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ShaderStack.Compose("s", [new StackFile("a.slang", Fragment), new StackFile("b.slang", Fragment)]))!;

        Assert.That(error.Message, Does.StartWith("s/b.slang(2): error:").And.Contain("a.slang"));
    }

    [TestCase("shade.txt")]
    [TestCase("my shade.slang")]
    [TestCase("../shade.slang")]
    public void AFileNeedsAPlainSlangName(string name)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderStack.Compose("s", [new StackFile(name, Fragment)]))!;

        Assert.That(error.Message, Does.Contain("error: a stack's file is a plain name"));
    }

    [Test]
    public void SlangsErrorsLandOnTheFileThatHasThem()
    {
        string body = ShaderStack.Compose("s", [new StackFile("bend.slang", Vertex), new StackFile("look.slang", "import xanadu;\n\nfloat4 shade(Canvas canvas) { return nope; }\n")]);
        Exception error = Assert.Catch(() => SlangShaders.CompileEffectBody("s", body))!;

        List<SlangProblem> onFragment = SlangProblems.Parse(error.Message, ShaderStack.Label("s", "look.slang"));
        Assert.That(onFragment, Has.Count.GreaterThan(0));
        Assert.That(onFragment[0].Line, Is.EqualTo(3));
        Assert.That(SlangProblems.Parse(error.Message, ShaderStack.Label("s", "bend.slang")).Where(problem => problem.Line > 0), Is.Empty);
    }

    [Test]
    public void ANewStackCompiles() =>
        Assert.DoesNotThrow(() => SlangShaders.CompileEffectBody("s", ShaderStack.Compose("s", [ShaderStack.NewStack()])));

    [Test]
    public void AFreeNameSkipsTheTakenOnes() =>
        Assert.That(ShaderStack.FreeName(["part.slang", "PART2.slang"]), Is.EqualTo("part3.slang"));
}
