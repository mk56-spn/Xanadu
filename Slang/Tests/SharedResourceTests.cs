using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary><c>[Shared]</c> lists and images: the frame's, whatever material declares them, writers before readers.</summary>
[TestFixture]
public sealed class SharedResourceTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("shared", ShaderStack.Compose("shared", [new StackFile("graph.slang", source)]));

    /// <summary>Writes the mask over its box and counts into the hits; reads neither back.</summary>
    private const string Writer = """
        import xanadu;

        [Shared] [Clear(0.5)] Image<float> mask;
        [Shared] [Size(16)] List<uint> hits;

        [Pixels(8, 8)]
        void paint(ItemInput i, Canvas canvas)
        {
            mask[uint2(i.pixel)] = length(canvas.centred) / 100;
            if (all(group.pixel == 0))
                InterlockedAdd(hits[0], 1);
        }

        void graph() { paint(); }
        """;

    /// <summary>Shows the mask under its own box, tinted by the hits.</summary>
    private const string Reader = """
        import xanadu;

        [Shared] [Clear(0.5)] Image<float> mask;
        [Shared] [Size(16)] List<uint> hits;
        Image<float4> shown;

        [Pixels(8, 8)]
        void look(ItemInput i, Canvas canvas)
        {
            shown[group.pixel] = float4(mask[uint2(i.pixel)], float(hits[0] % 7) / 7, 0, 1);
        }

        void graph() { look(); present(shown); }
        """;

    [Test]
    public void SharedDeclarationsAreTheFrames()
    {
        StackBuild build = Compile(Writer).Build!.Shape;

        Assert.That(build.Images.Single(image => image.Name == "mask").Shared, Is.True);
        Assert.That(build.Buffers.Single(buffer => buffer.Name == "hits").Shared, Is.True);
    }

    [Test]
    public void TheWriterWritesAndTheReaderReads()
    {
        SharedUse writer = Compile(Writer).Build!.Shared;
        SharedUse reader = Compile(Reader).Build!.Shared;

        Assert.That(writer.Writes, Is.EquivalentTo(new[] { "image mask r32f", "list hits uint" }));
        Assert.That(reader.Writes, Is.Empty);
        Assert.That(reader.Reads, Is.EquivalentTo(new[] { "image mask r32f", "list hits uint" }));
    }

    [Test]
    public void AGraphWithNothingSharedUsesNothing() =>
        Assert.That(Compile(Reader.Replace("[Shared] ", "").Replace("[Clear(0.5)] Image<float> mask;", "[Size(64, 64)] Image<float> mask;")).Build!.Shared.Any,
            Is.False);

    [Test]
    public void ASharedImageIsNotTheMaterials()
    {
        string touchedByAPass = Writer.Replace("void graph() { paint(); }", """
            [Pass(64, over: 64)]
            void wipe(uint i) { mask[uint2(i, 0)] = 0; }

            void graph() { wipe(); paint(); }
            """);
        CompiledShader compiled = Compile(touchedByAPass);
        int mask = compiled.Build!.Shape.Images.ToList().FindIndex(image => image.Name == "mask");

        Assert.That(compiled.Build.MaterialImages[mask], Is.False);
        Assert.That(GraphPlan.Images(compiled).Single(image => image.Name == "mask").Shared, Is.True);
    }

    [TestCase("[Shared] [Kept] Image<float> mask;", "can't be [Kept]")]
    [TestCase("[Shared] [Host] [Size(4)] List<uint> hits;", "can't be [Cached] or [Host]")]
    [TestCase("[Shared] Counter hits;", "takes no attributes")]
    public void SharedTakesNoLifeOfItsOwn(string declaration, string says)
    {
        string wrong = Writer.Replace("[Shared] [Size(16)] List<uint> hits;", declaration.Contains("mask") ? "[Shared] [Size(16)] List<uint> hits;" : declaration)
            .Replace("[Shared] [Clear(0.5)] Image<float> mask;", declaration.Contains("mask") ? declaration : "[Shared] [Clear(0.5)] Image<float> mask;");

        Assert.That(() => Compile(wrong), Throws.Exception.With.Message.Contains(says));
    }

    [Test]
    public void ADrawCantRenderIntoASharedImage()
    {
        string drawn = Writer.Replace("void graph() { paint(); }", """
            [Draw(1, 1, shade: flatLook)]
            ItemVertex flat(ItemVertex v) { return v; }
            float flatLook(ItemInput i) { return 1; }

            void graph() { flat(mask); }
            """);

        Assert.That(() => Compile(drawn), Throws.Exception.With.Message.Contains("is [Shared], the screen's"));
    }

    [Test]
    public void ASharedImageKeepsNoHistory()
    {
        string history = Writer.Replace("[Shared] [Size(16)]", "[History(mask)] Image<float> before;\n[Shared] [Size(16)]");

        Assert.That(() => Compile(history), Throws.Exception.With.Message.Contains("keeps no history"));
    }

    [Test]
    public void PresentingASharedImageReadsItAtThePixel()
    {
        string presented = Writer.Replace("[Shared] [Clear(0.5)] Image<float> mask;", "[Shared] Image<float4> tint;\n[Shared] [Clear(0.5)] Image<float> mask;")
            .Replace("void graph() { paint(); }", "void graph() { paint(); present(tint); }");

        Assert.That(Compile(presented).Build!.Passes.Any(pass => pass.Drawn is { CoversBox: true, Reads.Length: 1 }), Is.True);
    }
}
