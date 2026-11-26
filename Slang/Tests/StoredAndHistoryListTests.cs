using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary><c>Stored&lt;T&gt; name;</c> (one kept value, used by its plain name) and <c>[History(list)] List&lt;T&gt;</c> (a list's last frame).</summary>
[TestFixture]
public sealed class StoredAndHistoryListTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("stored", ShaderStack.Compose("stored", [new StackFile("graph.slang", source)]));

    private const string Graph = """
        import xanadu;

        struct Book { uint written; uint parity; float owed[4]; };

        Stored<Book> book;
        [Size(64)] List<float> field;
        [History(field)] List<float> before;

        [Pass(64, over: 64)]
        void spread(uint i)
        {
            if (i == 0)
                book.parity = 1 - book.parity;
            uint slot;
            InterlockedAdd(book.written, 1, slot);
            book.owed[i % 4] += 0.5;
            field[i] = before[i] * 0.9 + float(slot);
        }

        void graph() { spread(); }
        """;

    [Test]
    public void AStoredValueIsOneElementUsedByName()
    {
        StackBuild build = Compile(Graph).Build!.Shape;

        BuildBuffer book = build.Buffers.Single(buffer => buffer.Name == "book");
        Assert.That(book.Stored, Is.True);
        Assert.That(book.Count, Is.EqualTo(1));
        Assert.That(book.Type, Is.EqualTo("Book"));
    }

    [Test]
    public void AHistoryIsItsListsOtherHalf()
    {
        CompiledBuild built = Compile(Graph).Build!;
        int field = built.Shape.Buffers.ToList().FindIndex(buffer => buffer.Name == "field");
        int before = built.Shape.Buffers.ToList().FindIndex(buffer => buffer.Name == "before");

        Assert.That(built.Shape.Buffers[before].Source, Is.EqualTo(field));
        Assert.That(built.Bytes[before], Is.EqualTo(built.Bytes[field]));
    }

    [TestCase("[History(field)] [Size(8)] List<float> before;", "takes nothing but")]
    [TestCase("[History(nothing)] List<float> before;", "isn't a List")]
    [TestCase("[History(field)] List<uint> before;", "so its history does too")]
    [TestCase("[History(book)] List<Book> before;", "one stored value")]
    [TestCase("[History(field)] List<float> before; [History(field)] List<float> again;", "has a history already")]
    public void AWrongHistorySaysWhy(string declared, string said)
    {
        string source = Graph.Replace("[History(field)] List<float> before;", declared);
        Assert.That(() => Compile(source), Throws.Exception.With.Message.Contains(said));
    }

    [Test]
    public void AStoredValueTakesNoSize() =>
        Assert.That(() => Compile(Graph.Replace("Stored<Book> book;", "[Size(4)] Stored<Book> book;")),
            Throws.Exception.With.Message.Contains("doesn't go on a Stored"));
}
