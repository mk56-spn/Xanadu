using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary><c>[View]</c> images: the canvas drawn again by the host from a subtree the material names, read only.</summary>
[TestFixture]
public sealed class ViewImageTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("view", ShaderStack.Compose("view", [new StackFile("graph.slang", source)]));

    /// <summary>Shows the view under its own box.</summary>
    private const string Reader = """
        import xanadu;

        [View] Image<float4> mirrored;
        Image<float4> shown;

        [Pixels(8, 8)]
        void look(ItemInput i, Canvas canvas)
        {
            shown[group.pixel] = mirrored[uint2(i.pixel)];
        }

        void graph() { look(); present(shown); }
        """;

    [Test]
    public void AViewIsTheCanvasFormatAndTheScreens()
    {
        CompiledShader compiled = Compile(Reader);
        BuildImage view = compiled.Build!.Shape.Images.Single(image => image.Name == "mirrored");

        Assert.That(view.View, Is.True);
        Assert.That(view.Shared, Is.False);
        Assert.That(view.Format, Is.EqualTo("rgba8"));
        Assert.That(GraphPlan.Images(compiled).Single(image => image.Name == "mirrored").View, Is.True);
        Assert.That(compiled.Build.Shared.Any, Is.False);
    }

    [TestCase("[View] Image<float> mirrored;")]
    [TestCase("[View] [Scale(0.5)] Image<float4> mirrored;")]
    [TestCase("[View] [Kept] Image<float4> mirrored;")]
    [TestCase("[View] [Shared] Image<float4> mirrored;")]
    public void AViewTakesNothingElse(string declaration) =>
        Assert.That(() => Compile(Reader.Replace("[View] Image<float4> mirrored;", declaration)),
            Throws.Exception.With.Message.Contains("takes nothing else"));

    [Test]
    public void APassCantWriteAView()
    {
        string writes = Reader.Replace("shown[group.pixel] = mirrored[uint2(i.pixel)];", "mirrored[uint2(i.pixel)] = float4(1);\n    shown[group.pixel] = float4(0);");

        Assert.That(() => Compile(writes), Throws.Exception.With.Message.Contains("a [View]: the host draws it"));
    }

    [Test]
    public void ADrawCantRenderIntoAView()
    {
        string drawn = Reader.Replace("void graph() { look(); present(shown); }", """
            [Draw(1, 1, shade: flatLook)]
            ItemVertex flat(ItemVertex v) { return v; }
            float4 flatLook(ItemInput i) { return 1; }

            void graph() { flat(mirrored); present(shown); }
            """);

        Assert.That(() => Compile(drawn), Throws.Exception.With.Message.Contains("is a [View], drawn by the host"));
    }

    [Test]
    public void PresentingAViewReadsItAtThePixel() =>
        Assert.That(Compile(Reader.Replace("void graph() { look(); present(shown); }", "void graph() { present(mirrored); }"))
            .Build!.Passes.Any(pass => pass.Drawn is { CoversBox: true, Reads.Length: 1 }), Is.True);
}
