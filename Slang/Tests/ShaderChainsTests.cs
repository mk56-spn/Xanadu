using System.IO;
using System.Linq;
using NUnit.Framework;
using Xanadu.Slang;
using Xanadu.Storage;

namespace Xanadu.Slang.Tests;

[TestFixture]
public sealed class ShaderChainsTests
{
    private const string Library = """
        public extension<S : IShape> S
        {
            [mutating] public void rotate(float angle) { placement.angle += angle; }
            [mutating] public void move(float2 offset) { placement.offset += offset; }
            [mutating] public void lonely(float x) { }
            public S rotated(float angle) { S r = this; r.rotate(angle); return r; }
            public S moved(float2 offset) { S r = this; r.move(offset); return r; }
            public float filled(float soft = 1.0) { return 0; }
        }
        public extension float4
        {
            [mutating] public void paint<S : IShape>(S shape, colour top) { }
            public colour painted<S : IShape>(S shape, colour top) { return this; }
        }
        """;

    private static ShaderChains Chains() => ShaderChains.FromSources([Library]);

    [Test]
    public void PairsComeFromTheLibrarysMutatingMethodsWithTwins()
    {
        ShaderChains chains = Chains();

        Assert.That(chains.Twins["move"], Is.EqualTo("moved"));
        Assert.That(chains.Twins["rotate"], Is.EqualTo("rotated"));
        Assert.That(chains.Twins["paint"], Is.EqualTo("painted"));
        Assert.That(chains.Twins.ContainsKey("lonely"), Is.False);
    }

    [TestCase("let p = Box(s).move(o).rotate(t);", "let p = Box(s).moved(o).rotated(t);")]
    [TestCase("float a = Circle(30).move(float2(5, 0)).filled();", "float a = Circle(30).moved(float2(5, 0)).filled();")]
    [TestCase("x = colour(1).paint(Circle(2), y);", "x = colour(1).painted(Circle(2), y);")]
    public void InPlaceCallsOnATemporaryTakeTheCopy(string source, string expected) =>
        Assert.That(Chains().Rewrite(source), Is.EqualTo(expected));

    [TestCase("c.move(a).rotate(b);", "c = c.moved(a).rotated(b);")]
    [TestCase("    c.move(f(1, 2)).rotate(b).move(d);", "    c = c.moved(f(1, 2)).rotated(b).moved(d);")]
    [TestCase("{ s.ring.move(a).rotate(b); }", "{ s.ring = s.ring.moved(a).rotated(b); }")]
    [TestCase("if (on) c.move(a).rotate(b);", "if (on) c = c.moved(a).rotated(b);")]
    [TestCase("else c.move(a).rotate(b);", "else c = c.moved(a).rotated(b);")]
    [TestCase("shapes[i].move(a).rotate(b);", "shapes[i] = shapes[i].moved(a).rotated(b);")]
    [TestCase("c.paint(a, x).paint(b, y);", "c = c.painted(a, x).painted(b, y);")]
    public void AStatementChainingInPlaceCallsOnAVariableBecomesAnAssignment(string source, string expected) =>
        Assert.That(Chains().Rewrite(source), Is.EqualTo(expected));

    [TestCase("c.move(a);")]
    [TestCase("c.rotate(time);\nc.move(float2(1, 2));")]
    [TestCase("let b = a.moved(5);")]
    [TestCase("float v = c.move(a).filled();")]
    [TestCase("c.move(a).filled();")]
    [TestCase("// Circle(3).move(1).rotate(2);")]
    [TestCase("c.lonely(1);")]
    public void EverythingElseIsLeftAlone(string source) =>
        Assert.That(Chains().Rewrite(source), Is.EqualTo(source));

    [Test]
    public void LinesStayWhereTheyAre()
    {
        string source = "c.move(a).rotate(b);\nlet p = Box(s).move(o);\nreturn c;";

        Assert.That(Chains().Rewrite(source).Split('\n'), Has.Length.EqualTo(3));
    }

    [Test]
    public void TheShippedLibraryPairsItsPlacementAndPaint()
    {
        string folder = GamePaths.Full("Assets/Shaders/Library");
        ShaderChains chains = ShaderChains.FromSources(Directory.EnumerateFiles(folder, "*.slang", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.That(chains.Twins.Keys, Is.SupersetOf(new[] { "move", "rotate", "scale", "anchor", "paint" }));
    }
}
