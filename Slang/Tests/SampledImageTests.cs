using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary><c>sampled(image, at, size)</c>: a pixel pass reads a float4 image through the GPU's filter; elsewhere it filters by hand.</summary>
[TestFixture]
public sealed class SampledImageTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("sampled", ShaderStack.Compose("sampled", [new StackFile("graph.slang", source)]));

    /// <summary>A half-size glow, read back over the box through its sampler.</summary>
    private const string Reader = """
        import xanadu;

        [Scale(0.5)] Image<float4> glow;
        Image<float4> shown;

        [Pixels(8, 8, over: glow)]
        void soft(ItemInput i) { glow[group.pixel] = float4(i.uv, 0, 1); }

        [Pixels(8, 8)]
        void look(ItemInput i, Canvas canvas)
        {
            shown[group.pixel] = sampled(glow, (float2(group.pixel) + 0.5) / 2, ceil(canvas.size / 2));
        }

        void graph() { soft(); look(); present(shown); }
        """;

    private static CompiledPass Pass(CompiledShader compiled, string name) => compiled.Build!.Passes.Single(pass => pass.Name == name);

    [Test]
    public void APixelPassReadsTheImageThroughASampler()
    {
        CompiledShader compiled = Compile(Reader);
        int glow = compiled.Build!.Shape.Images.ToList().FindIndex(image => image.Name == "glow");
        CompiledPass look = Pass(compiled, "look");

        Assert.That(look.Sampled, Is.EqualTo(new[] { glow }));
        Assert.That(look.Images, Does.Not.Contain(glow));
        // Its sampler sits after the canvas's two; nothing is read where unplaced ones park.
        Assert.That(SpirvScan.Binds(look.Shader.Spirv, 0, SlangShaders.PixelSamplers), Is.True);
        Assert.That(SpirvScan.Binds(look.Shader.Spirv, 0, StackBuild.UnplacedSampler + glow), Is.False);
    }

    [Test]
    public void APassThatWritesTheImageItSamplesIsRefused()
    {
        string both = Reader.Replace("shown[group.pixel] = sampled(", "glow[group.pixel / 2] = 0;\n    shown[group.pixel] = sampled(");

        Assert.That(() => Compile(both), Throws.Exception.With.Message.Contains("touches it as an image too"));
    }

    [Test]
    public void AHandleGoesThroughAHelperAndKeepsTheSampler()
    {
        string helped = Reader.Replace("[Pixels(8, 8)]\nvoid look", """
            float4 twice(Sampled image, float2 at, float2 size) { return image.at(at, size) * 0.5 + image.at(at + 1, size) * 0.5; }

            [Pixels(8, 8)]
            void look
            """).Replace("sampled(glow, (float2(group.pixel) + 0.5) / 2, ceil(canvas.size / 2))",
            "twice(sampled(glow), (float2(group.pixel) + 0.5) / 2, ceil(canvas.size / 2))");
        CompiledShader compiled = Compile(helped);
        CompiledPass look = Pass(compiled, "look");

        Assert.That(look.Sampled, Has.Length.EqualTo(1));
        Assert.That(SpirvScan.Binds(look.Shader.Spirv, 0, SlangShaders.PixelSamplers), Is.True);
    }

    [Test]
    public void AHandleOutsideAPixelPassIsRefused()
    {
        string drawn = Reader.Replace("void graph() { soft(); look(); present(shown); }", """
            [Draw(1, 1, shade: flatLook)]
            ItemVertex flat(ItemVertex v) { return v; }
            float4 flatLook(ItemInput i) { return sampled(glow).at(i.uv * 4, float2(4)); }

            void graph() { soft(); look(); flat(shown); present(shown); }
            """);

        Assert.That(() => Compile(drawn), Throws.Exception.With.Message.Contains("only in a [Pixels] pass"));
    }

    [Test]
    public void OutsideAPixelPassItFiltersByHand()
    {
        string drawn = Reader.Replace("void graph() { soft(); look(); present(shown); }", """
            [Draw(1, 1, shade: flatLook)]
            ItemVertex flat(ItemVertex v) { return v; }
            float4 flatLook(ItemInput i) { return sampled(glow, i.uv * 4, float2(4)); }

            void graph() { soft(); look(); flat(shown); present(shown); }
            """);

        Assert.That(Compile(drawn).Build!.Passes.Single(pass => pass.Drawn is not null && pass.Name == "flat").Drawn!.Reads, Is.Not.Empty);
    }
}
