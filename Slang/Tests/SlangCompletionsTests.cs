using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// The code editor's completion, built from the shipped library as the lab
/// builds it. Each case is the text before the caret on the last line of
/// some code, and what should (or shouldn't) be offered there.
/// </summary>
[TestFixture]
public sealed class SlangCompletionsTests
{
    /// <summary>A struct the way a preview's prelude declares one (the spawner's Thing).</summary>
    private const string Prelude = """
        /// One thing.
        public struct Thing
        {
            /// Where it is.
            public float2 position;
            /// What colour it is.
            public colour colour;
            /// How big it is.
            public float2 size;
        };
        """;

    private SlangCompletions _completions = null!;

    [OneTimeSetUp]
    public void Build()
    {
        ShaderConstants constants = ShaderConstants.FromSources(ShippedLibrary.Sources);
        _completions = SlangCompletions.FromSources(
            ShippedLibrary.Sources.Append(Prelude), constants, [("time", "float", "Seconds."), ("mouse", "float2", null)]);
    }

    /// <summary>What's offered with the caret at the end of the last line of <paramref name="code"/>.</summary>
    private List<Completion>? At(string code)
    {
        string[] lines = code.ReplaceLineEndings("\n").Split('\n');
        return _completions.At(lines[^1], lines, out _, lines.Length - 1);
    }

    private List<string> Names(string code) => At(code)?.Select(c => c.Name).ToList() ?? [];

    [Test]
    public void ALocalColourGetsTheNamedColours()
    {
        List<Completion>? offered = At("colour c = colours.black;\nc = ");

        Assert.That(offered, Is.Not.Null);
        Assert.That(offered!.Select(c => c.Text), Does.Contain("colours.gold"));
    }

    [Test]
    public void ADeclarationOfAColourGetsTheNamedColours()
    {
        Assert.That(At("colour c = ")?.Select(c => c.Text), Does.Contain("colours.hotPink"));
    }

    [Test]
    public void AColourFieldOfAStructGetsTheNamedColours()
    {
        List<Completion>? offered = At("Thing t;\nt.colour = ");

        Assert.That(offered, Is.Not.Null, "t.colour is declared colour, so a colour is being given");
        Assert.That(offered!.Select(c => c.Text), Does.Contain("colours.gold"));
    }

    [Test]
    public void ANonColourFieldDoesNotGetTheNamedColours()
    {
        Assert.That(At("Thing t;\nt.position = ")?.Select(c => c.Text) ?? [], Does.Not.Contain("colours.gold"));
    }

    [Test]
    public void AComparisonIsNotAnAssignment()
    {
        Assert.That(At("colour c = colours.black;\nif (c == ")?.Select(c => c.Text) ?? [], Does.Not.Contain("colours.gold"));
    }

    [Test]
    public void TypedLettersNarrowTheColours()
    {
        List<string?> texts = At("colour c = go")?.Select(c => c.Text).ToList() ?? [];

        Assert.That(texts, Does.Contain("colours.gold"));
        Assert.That(texts, Does.Not.Contain("colours.hotPink"));
    }

    [Test]
    public void APreludeStructsFieldsComeAfterADot()
    {
        Assert.That(Names("Thing t;\nt."), Is.SupersetOf(new[] { "position", "colour", "size" }));
    }

    [Test]
    public void ALibraryStructsFieldsComeAfterADot()
    {
        Assert.That(Names("float4 shade(Canvas canvas)\n{\n    canvas."), Is.SupersetOf(new[] { "size", "centred", "aspect" }));
    }

    [Test]
    public void AChainWalksThroughFieldTypes()
    {
        // ItemInput.canvas is a Canvas.
        Assert.That(Names("float4 shade(ItemInput i)\n{\n    i.canvas."), Does.Contain("centred"));
    }

    [Test]
    public void ANamespacesMembersComeAfterItsDot()
    {
        Assert.That(Names("colour c = colours.black;\nease."), Is.Not.Empty);
    }

    [Test]
    public void ShapesComeUpWhereAValueStarts()
    {
        List<Completion>? offered = At("float4 shade()\n{\n    let s = ");

        Assert.That(offered?.Select(c => c.Name), Does.Contain("Circle"));
        Assert.That(offered!.First(c => c.Name == "Circle").Text, Is.EqualTo("Circle($)"));
    }

    [Test]
    public void ShapesGetTheirMethodsAfterADot()
    {
        Assert.That(Names("float4 shade()\n{\n    let s = Circle(40);\n    s."), Is.SupersetOf(new[] { "filled", "moved", "rotated" }));
    }

    [Test]
    public void ProvidedValuesComeUpWhileTyping()
    {
        Assert.That(Names("float4 shade()\n{\n    float t = ti"), Does.Contain("time"));
    }

    [Test]
    public void NothingIsOfferedInAComment()
    {
        Assert.That(At("// colour c = "), Is.Null);
    }

    [Test]
    public void ADotAfterANumberIsNotAMemberAccess()
    {
        Assert.That(At("float x = 1."), Is.Null);
    }

    [Test]
    public void ShadeIsOfferedWholeAtTheTopLevelUntilItIsDeclared()
    {
        static bool Snippet(Completion c) => c is { Name: "shade", Kind: CompletionKind.Snippet };

        Assert.That(At("import xanadu;\nsha")!.Exists(Snippet), Is.True);
        // Once written, shade is still offered, as one of the code's own functions; just not whole again.
        Assert.That(At("float4 shade() { return 1; }\nsha")?.Exists(Snippet) ?? false, Is.False);
    }

    [Test]
    public void ForIsOfferedWholeInsideABodyEvenOnceTyped()
    {
        static Completion? For(List<Completion>? found) =>
            found?.Find(c => c is { Name: "for", Kind: CompletionKind.Snippet });

        Completion? typed = For(At("void update(inout State s, float dt)\n{\n    for"));
        Assert.That(typed?.Text, Is.EqualTo("for (int i = 0; i < $; i++)\n    {\n        \n    }"));
        Assert.That(For(At("float4 shade(Canvas canvas)\n{\n    fo")), Is.Not.Null);
        // At the top level a for is no statement.
        Assert.That(For(At("import xanadu;\nfor")), Is.Null);
    }

    [Test]
    public void IfIsOfferedWholeInsideABody()
    {
        Completion? found = At("float4 shade(Canvas canvas)\n{\n    if")?.Find(c => c is { Name: "if", Kind: CompletionKind.Snippet });
        Assert.That(found?.Text, Is.EqualTo("if ($)\n    {\n        \n    }"));
    }

    [Test]
    public void UniformsAreOfferedWholeAtTheTopLevelWithTheirSliderDefault()
    {
        List<string?> texts = (At("import xanadu;\nuni") ?? [])
            .Where(c => c.Kind == CompletionKind.Snippet).Select(c => c.Text).ToList();
        Assert.That(texts, Does.Contain("[Default(1)] uniform float $;"));
        Assert.That(texts, Does.Contain("[Default(colours.white)] uniform colour $;"));

        // u also starts update, which is offered beside them.
        Assert.That(At("import xanadu;\nu")!.Exists(c => c.Name == "update"), Is.True);
        Assert.That(At("import xanadu;\nu")!.Exists(c => c.Name == "uniform" && c.Kind == CompletionKind.Snippet), Is.True);
        Assert.That(At("float4 shade(Canvas canvas)\n{\n    uniform")?.Exists(c => c.Kind == CompletionKind.Snippet) ?? false, Is.False);
    }

    [Test]
    public void ALibraryTypeAndADotOfferWhatItHas()
    {
        List<string> names = At("float4 shade(Canvas canvas)\n{\n    Canvas.")!.ConvertAll(c => c.Name);
        Assert.That(names, Does.Contain("centred"));
        Assert.That(names, Does.Contain("uvAt"));
    }

    [Test]
    public void HoveringALibraryTypeOrSomethingOfItListsWhatItHas()
    {
        string[] code = ["float4 shade(Canvas canvas)", "{", "    return canvas.size.x;", "}"];
        string? type = _completions.Describe(code[0], 15, code, out _)?[^1].Doc;
        Assert.That(type, Does.Contain("Has:"));
        Assert.That(type, Does.Contain("    float2 centred"));

        string? variable = _completions.Describe(code[2], 12, code, out _)?[^1].Doc;
        Assert.That(variable, Does.Contain("    float2 centred"));

        // A shape lists its own members, then the ones every shape shares.
        string? box = _completions.Describe("Box b = Box(10);", 1, ["Box b = Box(10);"], out _)?[^1].Doc;
        Assert.That(box, Does.Contain("Also every IShape's:"));
    }
}
