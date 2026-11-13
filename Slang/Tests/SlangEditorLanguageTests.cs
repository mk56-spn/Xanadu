using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Reading Slang's errors back onto the author's lines (rustc style and the one-line style).</summary>
[TestFixture]
public sealed class SlangProblemsTests
{
    [Test]
    public void AVectorComparisonUsedAsAConditionIsExplained()
    {
        const string output = """
            error[E30019]: type mismatch in expression
             --> mine:5:11
              |
            5 | if (a != b)
              |       ^^ expected an expression of type 'bool', got 'vector<bool,2>'
            """;
        SlangProblem problem = SlangProblems.Parse(output, "mine").Single();
        Assert.That(problem.Line, Is.EqualTo(5));
        Assert.That(problem.Message, Does.Contain("any(…)"));
    }

    [Test]
    public void TheRustcStyleGivesLineColumnAndTheCaretsWords()
    {
        const string output = """
            error[E30015]: undefined identifier
             --> mine:4:20
              |
            4 | let c = Circle(radius: 20.0);
              |                ^^^^^^ undefined identifier 'radius'.
            """;

        SlangProblem problem = SlangProblems.Parse(output, "mine").Single();

        Assert.That(problem.Line, Is.EqualTo(4));
        Assert.That(problem.Column, Is.EqualTo(20));
        Assert.That(problem.Warning, Is.False);
        Assert.That(problem.Message, Does.Contain("radius"));
    }

    [Test]
    public void TheOneLineStyleGivesTheLine()
    {
        SlangProblem problem = SlangProblems.Parse("mine(6): error 10005: newline in literal", "mine").Single();

        Assert.That(problem.Line, Is.EqualTo(6));
        Assert.That(problem.Message, Is.EqualTo("newline in literal"));
    }

    [Test]
    public void AWarningStaysAWarning()
    {
        Assert.That(SlangProblems.Parse("mine(2): warning 15205: unused", "mine").Single().Warning, Is.True);
    }

    [Test]
    public void AProblemInTheWrapperHasNoLineOfTheAuthors()
    {
        // Line 0: the editor lists it, but can't point into the code at it.
        SlangProblem problem = SlangProblems.Parse("mine (wrapper)(3): error 1: wrapped", "mine").Single();

        Assert.That(problem.Line, Is.Zero);
    }

    [Test]
    public void EmptyOutputGivesNoProblems()
    {
        Assert.That(SlangProblems.Parse("", "mine"), Is.Empty);
    }

    [Test]
    public void OutputItCantReadStillGivesOneProblem()
    {
        // Parse only sees a failed compile's output, so whatever it says must show up.
        SlangProblem problem = SlangProblems.Parse("spawner: say how many things there are\nmore", "mine").Single();

        Assert.That(problem.Line, Is.Zero);
        Assert.That(problem.Message, Is.EqualTo("spawner: say how many things there are"));
    }

    [Test]
    public void AMissingSemicolonGoesBackToTheLineThatLacksIt()
    {
        const string output = """
            error[E20001]: unexpected identifier, expected ';'
             --> mine:4:5
              |
            4 |     return c;
              |     ^^^^^^ expected ';'
            """;
        string[] code = ["float4 shade()", "{", "    colour c = 1  // no semicolon", "    return c;", "}"];

        SlangProblem problem = SlangProblems.Parse(output, "mine", code).Single();

        Assert.That(problem.Line, Is.EqualTo(3));
        Assert.That(problem.Message, Is.EqualTo("missing ';' at the end of this line"));
    }
}

/// <summary>The editor's own warnings, which Slang doesn't give.</summary>
[TestFixture]
public sealed class SlangLintTests
{
    private static readonly IReadOnlySet<string> Types = new HashSet<string>(SlangSyntax.BuiltInTypes) { "colour" };

    [Test]
    public void ALocalUsedBeforeItHasAValueIsFlagged()
    {
        List<SlangProblem> problems = SlangLint.Check("""
            float4 shade()
            {
                colour c;
                return c;
            }
            """, Types);

        SlangProblem problem = problems.Single();
        Assert.That(problem.Message, Does.StartWith("XS003"));
        Assert.That(problem.Line, Is.EqualTo(3));
        Assert.That(problem.Warning, Is.True);
    }

    [Test]
    public void ALocalGivenAValueFirstIsFine()
    {
        Assert.That(SlangLint.Check("""
            float4 shade()
            {
                colour c;
                c = float4(1, 1, 1, 1);
                return c;
            }
            """, Types), Is.Empty);
    }

    [Test]
    public void ALocalWhoseFieldIsSetFirstIsFine()
    {
        Assert.That(SlangLint.Check("""
            float4 shade()
            {
                colour c;
                c.r = 1;
                return c;
            }
            """, Types), Is.Empty);
    }

    [Test]
    public void FileLevelDeclarationsAreTheHostsToSet()
    {
        Assert.That(SlangLint.Check("uniform colour tint;\nfloat4 shade() { return tint; }", Types), Is.Empty);
    }

    [Test]
    public void AWholeNumberDivisionWhereAFloatIsWantedIsFlagged()
    {
        const string code = "static const int trail = 30;\nfloat4 shade()\n{\n    float step = 1 / trail;\n    return step;\n}";
        SlangProblem problem = SlangLint.Check(code, Types).Single();
        Assert.That(problem.Message, Does.StartWith("XS002"));
        Assert.That(problem.Message, Does.Contain("1.0 / trail"));
        Assert.That((problem.Line, problem.Column), Is.EqualTo((4, 18)));

        Assert.That(SlangLint.Check("float4 shade(int count)\n{\n    return float4(count / 3);\n}", Types).Single().Message,
            Does.Contain("float(count) / 3"));
    }

    [Test]
    public void FloatDivisionsAndOnesThatWantAnIntAreFine()
    {
        Assert.That(SlangLint.Check("""
            static const int trail = 30;
            int half(int n) { return n / 2; }
            float4 shade(float x)
            {
                float a = 1. / trail;
                float b = x / 2;
                float c = float(trail) / 2;
                int mid = trail / 2;
                int pts[trail];
                int e = pts[trail / 2];
                for (int i = 0; i < trail / 2; i++)
                    a += 1;
                return a + b + c;
            }
            """, Types), Is.Empty);
    }

    [Test]
    public void MouseGivenToAnchorIsFlagged()
    {
        SlangProblem problem = SlangLint.Check("float4 shade() { return Circle(9).anchored(mouse).filled(); }", Types).Single();

        Assert.That(problem.Message, Does.StartWith("XS004"));
        Assert.That(problem.Message, Does.Contain("moved(mouse)"));
    }
}

/// <summary>F2: every place a name means the same thing. Lines and columns from 0.</summary>
[TestFixture]
public sealed class SlangRenameTests
{
    private static List<(int Line, int Column)>? Places(string code, int line, int column) =>
        SlangRename.Find(code.Split('\n'), line, column)?.Places;

    [Test]
    public void ALocalIsRenamedOnlyInItsFunction()
    {
        const string code = """
            float a() { float x = 1; return x; }
            float b() { float x = 2; return x; }
            """;

        Assert.That(Places(code, 0, 18), Is.EqualTo(new[] { (0, 18), (0, 32) }));
    }

    [Test]
    public void AUniformIsRenamedEverywhereButWhereItIsShadowed()
    {
        const string code = """
            uniform float speed;
            float a() { return speed; }
            float b() { float speed = 1; return speed; }
            """;

        List<(int Line, int Column)>? places = Places(code, 0, 14);

        Assert.That(places, Is.EqualTo(new[] { (0, 14), (1, 19) }));
    }

    [Test]
    public void AFieldIsRenamedAtEveryDot()
    {
        const string code = """
            struct State { float2 light; };
            float a(State s) { return s.light.x; }
            """;

        Assert.That(Places(code, 1, 28)?.Select(p => p.Line), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void ANameTheCodeDoesntDeclareCantBeRenamed()
    {
        Assert.That(SlangRename.Find(["float a() { return sin(1); }"], 0, 20), Is.Null);
    }

    [Test]
    public void NamesInCommentsAreLeftAlone()
    {
        const string code = """
            uniform float speed; // speed is fast
            float a() { return speed; }
            """;

        Assert.That(Places(code, 0, 14)?.Count, Is.EqualTo(2));
    }

    [Test]
    public void ALocalGoesToItsOwnFunctionsDeclaration()
    {
        const string code = """
            float a() { float x = 1; return x; }
            float b() { float x = 2; return x; }
            """;

        Assert.That(SlangRename.Declaration(code.Split('\n'), 1, 32), Is.EqualTo((1, 18)));
    }

    [Test]
    public void AShadowedUniformGoesToTheLocal()
    {
        const string code = """
            uniform float speed;
            float a() { return speed; }
            float b() { float speed = 1; return speed; }
            """;

        string[] lines = code.Split('\n');
        Assert.That(SlangRename.Declaration(lines, 1, 20), Is.EqualTo((0, 14)));
        Assert.That(SlangRename.Declaration(lines, 2, 37), Is.EqualTo((2, 18)));
    }

    [Test]
    public void AFieldGoesToItsStruct()
    {
        const string code = """
            struct State { float2 light; };
            float a(State s) { return s.light.x; }
            """;

        string[] lines = code.Split('\n');
        Assert.That(SlangRename.Declaration(lines, 1, 28), Is.EqualTo((0, 22)));
        Assert.That(SlangRename.Declaration(lines, 1, 9), Is.EqualTo((0, 7)));
    }

    [Test]
    public void ALibraryNameHasNoDeclarationHere() =>
        Assert.That(SlangRename.Declaration(["float a() { return sin(1); }"], 0, 20), Is.Null);
}

/// <summary>Colouring: tokens, and brackets paired with their owners.</summary>
[TestFixture]
public sealed class SlangSyntaxTests
{
    private static readonly IReadOnlySet<string> LibraryTypes = new HashSet<string> { "Circle" };
    private static readonly IReadOnlySet<string> Namespaces = new HashSet<string> { "colours" };

    private static List<Token> Tokens(string line) => SlangSyntax.Tokens(line, LibraryTypes, Namespaces);

    private static TokenKind KindAt(string line, string word)
    {
        int at = line.IndexOf(word, System.StringComparison.Ordinal);
        return Tokens(line).First(t => t.Start <= at && at < t.End).Kind;
    }

    [Test]
    public void WordsAreSortedIntoKinds()
    {
        const string line = "return Circle(4.5).filled() * colours.gold; // done";

        Assert.That(KindAt(line, "return"), Is.EqualTo(TokenKind.Keyword));
        Assert.That(KindAt(line, "Circle"), Is.EqualTo(TokenKind.LibraryType));
        Assert.That(KindAt(line, "4.5"), Is.EqualTo(TokenKind.Number));
        Assert.That(KindAt(line, "colours"), Is.EqualTo(TokenKind.Namespace));
        Assert.That(KindAt(line, "// done"), Is.EqualTo(TokenKind.Comment));
    }

    [Test]
    public void TheLibrarysAttributesStandApartFromSlangs()
    {
        var ours = new HashSet<string> { "Instance", "Default" };
        List<Token> tokens = SlangSyntax.Tokens("[Instance] [shader(\"vertex\")] [Default(1)]", LibraryTypes, Namespaces, attributes: ours);

        Assert.That(tokens.Where(t => t.Kind is TokenKind.Attribute or TokenKind.LibraryAttribute).Select(t => t.Kind),
            Is.EqualTo(new[] { TokenKind.LibraryAttribute, TokenKind.Attribute, TokenKind.LibraryAttribute }));
    }

    [Test]
    public void HintsFindTheLibrarysAttributesByTheirShortNames()
    {
        var hints = new SlangHints();
        hints.Read("[__AttributeUsage(_AttributeTargets.Function)]\npublic struct InstanceAttribute\n{\n};\npublic struct Attribute {};");

        Assert.That(hints.Attributes, Is.EquivalentTo(new[] { "Instance" }));
    }

    [Test]
    public void TokensDontOverlapAndStayInOrder()
    {
        List<Token> tokens = Tokens("float4 c = lerp(a, b, 0.5); /* x */");

        for (int i = 1; i < tokens.Count; i++)
            Assert.That(tokens[i].Start, Is.GreaterThanOrEqualTo(tokens[i - 1].End));
    }

    [Test]
    public void BracketsPairAcrossLinesAndIgnoreComments()
    {
        string[] lines = ["float a()", "{", "    // (", "    return (1 + 2);", "}"];

        List<Bracket> brackets = SlangBrackets.Find(lines, Tokens);

        Assert.That(brackets.TrueForAll(b => b.Matched), Is.True);
        Assert.That(brackets.Exists(b => b.Line == 2), Is.False, "a bracket in a comment doesn't count");
        Bracket open = brackets.Single(b => b.Line == 1);
        Assert.That(brackets[open.Partner].Line, Is.EqualTo(4));
    }

    [Test]
    public void AnUnclosedBracketIsUnmatched()
    {
        Assert.That(SlangBrackets.Find(["float a(", "{"], Tokens).Exists(b => !b.Matched), Is.True);
    }

    [Test]
    public void ALibraryTypesBracketBelongsToIt()
    {
        List<Bracket> brackets = SlangBrackets.Find(["let s = Circle(4);"], Tokens);

        Assert.That(brackets[0].Owner, Is.EqualTo(TokenKind.LibraryType));
    }
}

/// <summary>Parameter names shown as hints in calls into the library.</summary>
[TestFixture]
public sealed class SlangHintsTests
{
    private SlangHints _hints = null!;

    [OneTimeSetUp]
    public void Read()
    {
        _hints = new SlangHints();
        _hints.Read("""
            public struct Box { public __init(float2 size, float radius = 0) { } };
            public float smooth(float edge, float x) { return x; }
            public typealias colour = float4;
            public namespace ease { public float inOut(float t) { return t; } }
            """);
    }

    [Test]
    public void TheLibrarysTypesAliasesAndNamespacesAreKnown()
    {
        Assert.That(_hints.Types, Does.Contain("Box"));
        Assert.That(_hints.Aliases, Does.Contain("colour"));
        Assert.That(_hints.Namespaces, Does.Contain("ease"));
    }

    [Test]
    public void ACallGetsAHintPerArgument()
    {
        List<InlayHint> hints = _hints.For("let b = Box(float2(10, 20), r);");

        Assert.That(hints.Select(h => h.Label), Is.EqualTo(new[] { "size:", "radius:" }));
    }

    [Test]
    public void AFunctionsArgumentsGetHintsToo()
    {
        Assert.That(_hints.For("float a = smooth(e, v);").Select(h => h.Label), Is.EqualTo(new[] { "edge:", "x:" }));
    }

    [Test]
    public void ACallInACommentGetsNone()
    {
        Assert.That(_hints.For("// Box(float2(1), 2)"), Is.Empty);
    }

    [Test]
    public void TheShippedLibraryHasHints()
    {
        SlangHints shipped = SlangHints.FromLibrary(System.IO.Path.Combine(ShippedLibrary.Path, "xanadu"));

        Assert.That(shipped.Count, Is.GreaterThan(20));
        Assert.That(shipped.Types, Does.Contain("Circle"));
    }
}

[TestFixture]
public sealed class SlangUnusedTests
{
    private static Dictionary<string, string> Faded(string code)
    {
        string[] lines = code.Split('\n');
        return SlangUnused.Find(lines).ToDictionary(f => lines[f.Line].Substring(f.Column, f.Length), f => f.Reason);
    }

    /// <summary>The trail shader that waited on a time it never kept.</summary>
    private const string Trail = """
        import xanadu;

        const int refreshSpeed;

        uniform colour circleColour;

        static const int trail = 10;

        struct State
        {
            float2[trail] prevPoints;
            float[trail] prevTime;
            int pointer = 0;
            float2 moved(float dt) { float2 step = prevPoints[0] * dt; return step; }
        };

        void update(inout State s, float dt)
        {
            if (s.prevTime[s.pointer] + 2 < time)
            {
                s.prevPoints[s.pointer] = mouse;
                s.pointer += 1;
            }
        }

        float4 shade(ItemInput i)
        {
            colour col = colours.transparent;
            for (int k = 0; k < trail; k++)
                col.paint(Circle(9).moved(state.prevPoints[k]), colours.red);
            return col;
        }
        """;

    [Test]
    public void AFieldReadButNeverGivenAValueFades()
    {
        Assert.That(Faded(Trail).GetValueOrDefault("prevTime"), Is.EqualTo(SlangUnused.NeverGiven));
    }

    [Test]
    public void NamesNeverUsedFade()
    {
        var faded = Faded(Trail);
        Assert.That(faded.GetValueOrDefault("refreshSpeed"), Is.EqualTo(SlangUnused.NeverUsed));
        Assert.That(faded.GetValueOrDefault("circleColour"), Is.EqualTo(SlangUnused.NeverUsed));
        Assert.That(faded.GetValueOrDefault("dt"), Is.EqualTo(SlangUnused.NeverUsed));
    }

    [Test]
    public void WhatIsReadAndWrittenOrCalledByTheHostStaysLit()
    {
        var faded = Faded(Trail);
        Assert.That(faded.Keys, Has.None.AnyOf("prevPoints", "pointer", "trail", "col", "s", "k", "shade", "update", "step"));
    }

    [Test]
    public void ALocalGivenValuesNobodyReadsFades()
    {
        var faded = Faded("float4 shade()\n{\n    float x = 1;\n    x = 2;\n    return 1;\n}");
        Assert.That(faded.GetValueOrDefault("x"), Is.EqualTo(SlangUnused.NeverRead));
    }

    [Test]
    public void AMutatingCallCountsAsGivingAValue()
    {
        const string code = "struct State\n{\n    colour tint;\n};\nfloat4 shade(State s)\n{\n    s.tint.paint(Circle(4), colours.red);\n    return s.tint;\n}";
        Assert.That(Faded(code).ContainsKey("tint"), Is.False);
    }
}

[TestFixture]
public sealed class SlangLoopsTests
{
    [Test]
    public void TheIndexIsFoundFromTheHeaderToTheBodysEnd()
    {
        const string code = "for (int i = 0; i < 4; i++)\n{\n    a[i] = s.i + i;\n}\nb = i;";
        var found = SlangLoops.Find(code.Split('\n'));
        // Header: three; body: a[i] and the last i, not s.i; nothing after the loop.
        Assert.That(found.Select(f => (f.Line, f.Column)), Is.EqualTo(new[] { (0, 9), (0, 16), (0, 23), (2, 6), (2, 17) }));
        Assert.That(found.TrueForAll(f => f.Depth == 0), Is.True);
    }

    [Test]
    public void ANestedLoopIsOneDeeperAndAOneStatementBodyEndsAtItsSemicolon()
    {
        const string code = "for (int i = 0; i < 4; i++)\n    for (int j = 0; j < i; j++)\n        x += j;\ny = j;";
        var found = SlangLoops.Find(code.Split('\n'));
        Assert.That(found.Where(f => f.Depth == 1).Select(f => f.Line), Is.EqualTo(new[] { 1, 1, 1, 2 }));
        Assert.That(found.Exists(f => f.Line == 3), Is.False);
        // i inside the inner header is still the outer loop's.
        Assert.That(found.Exists(f => f is { Line: 1, Depth: 0 }), Is.True);
    }

    [Test]
    public void ShadowedNamesBelongToTheInnerLoop()
    {
        const string code = "for (int i = 0; i < 2; i++)\n{\n    for (int i = 0; i < 3; i++)\n        x += i;\n}";
        var found = SlangLoops.Find(code.Split('\n'));
        Assert.That(found.Where(f => f.Line >= 2).All(f => f.Depth == 1), Is.True);
    }
}


public sealed class SlangLibraryDeclarationsTests
{
    private static readonly SlangLibraryDeclarations Library = SlangLibraryDeclarations.FromModules(
    [
        ("lib/xanadu/ease.slang", """
            // ease.out(t) eases
            module ease;
            public namespace ease
            {
                public float out(float t) { return t; }
            }
            public float out(float t) { return 1 - t; }
            """),
        ("lib/xanadu/anim.slang", """
            module anim;
            public struct Clip : IThing
            {
                public float start;
                public __init(float s) { start = s; }
            }
            public static const float TAU = 6.28;
            """),
    ]);

    [Test]
    public void ANameAfterADotGoesToTheOneInsideWhatIsBeforeIt() =>
        Assert.That(Library.Find("x = ease.out(t);", 10), Is.EqualTo(new LibraryPlace("lib/xanadu/ease.slang", 4, 17)));

    [Test]
    public void APlainNameGoesToTheTopOne() =>
        Assert.That(Library.Find("x = out(t);", 5), Is.EqualTo(new LibraryPlace("lib/xanadu/ease.slang", 6, 13)));

    [Test]
    public void StructsFieldsConstantsAndModulesAreFound()
    {
        Assert.That(Library.Find("Clip c;", 1), Is.EqualTo(new LibraryPlace("lib/xanadu/anim.slang", 1, 14)));
        Assert.That(Library.Find("c.start", 3), Is.EqualTo(new LibraryPlace("lib/xanadu/anim.slang", 3, 17)));
        Assert.That(Library.Find("float t = TAU;", 11), Is.EqualTo(new LibraryPlace("lib/xanadu/anim.slang", 6, 26)));
        Assert.That(Library.Find("import xanadu.anim;", 15), Is.EqualTo(new LibraryPlace("lib/xanadu/anim.slang", 0, 0)));
    }

    [Test]
    public void AnAttributeGoesToItsStruct() =>
        Assert.That(SlangLibraryDeclarations.FromModules([("lib/stage.slang", "public struct InstanceAttribute\n{\n};")]).Find("[Instance]", 3),
            Is.EqualTo(new LibraryPlace("lib/stage.slang", 0, 14)));

    [Test]
    public void NamesInCommentsAndUnknownNamesAreNot()
    {
        Assert.That(Library.Find("x = sin(t);", 5), Is.Null);
        Assert.That(Library.Find("x = eases(t);", 5), Is.Null);
    }
}
