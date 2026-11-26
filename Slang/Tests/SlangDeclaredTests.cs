using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>A global is coloured where it means the global, never where a field or local of the same name stands.</summary>
[TestFixture]
public sealed class SlangDeclaredTests
{
    private static readonly HashSet<string> None = [];

    private static readonly string[] Code =
    [
        "[Default(0.4)] uniform float spread;",
        "struct Emitter",
        "{",
        "    float spread;",
        "};",
        "float f(Emitter e)",
        "{",
        "    e.spread = spread;",
        "    float speed = 1;",
        "    return speed;",
        "}",
    ];

    /// <summary>The columns of line <paramref name="line"/> marked as a global.</summary>
    private static List<int> Globals(SlangDeclared declared, IReadOnlyList<string> lines, int line) =>
        [.. declared.Mark(line, SlangSyntax.Tokens(lines[line], None, None, code: declared)).Where(t => t.Kind == TokenKind.Global).Select(t => t.Start)];

    [Test]
    public void AFieldOfTheGlobalsNameIsNotTheGlobal()
    {
        SlangDeclared declared = SlangDeclared.Find(Code);
        Assert.That(Globals(declared, Code, 0), Is.EqualTo(new[] { 29 }));
        Assert.That(Globals(declared, Code, 3), Is.Empty);
        // e.spread is the field; the spread it is given is the global.
        Assert.That(Globals(declared, Code, 7), Is.EqualTo(new[] { 15 }));
    }

    [Test]
    public void AnotherFilesGlobalIsMarkedWhereThisFileDoesNotDeclareTheName()
    {
        string[] look = ["float g() { return speed + other; }", "float h() { float other = 2; return other; }"];
        SlangDeclared declared = SlangDeclared.Find(look, ["uniform float speed;", "uniform float other;"]);
        Assert.That(Globals(declared, look, 0), Is.EqualTo(new[] { 19, 27 }));
        Assert.That(Globals(declared, look, 1), Is.Empty);
    }
}
