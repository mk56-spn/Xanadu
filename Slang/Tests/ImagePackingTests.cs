using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>A draw with more outputs than a render pass has targets: its narrow ones share textures' channels.</summary>
[TestFixture]
public sealed class ImagePackingTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static CompiledShader Compile(string source) =>
        SlangShaders.CompileEffectBody("packing", ShaderStack.Compose("packing", [new StackFile("graph.slang", source)]));

    private const string Six = """
        import xanadu;

        Image<float> a;
        [Clear(1)] Image<float> b;
        Image<float> c;
        Image<float> d;
        Image<float2> e;
        Image<uint> f;
        Image<float4> shown;

        struct Six { float a; float b; float c; float d; float2 e; uint f; };

        [Draw(4, 4, shade: look)]
        ItemVertex flat(ItemVertex v) { return v; }

        Six look(ItemInput i)
        {
            Six s;
            s.a = 0.1; s.b = 0.2; s.c = 0.3; s.d = 0.4; s.e = i.uv; s.f = 7;
            return s;
        }

        [Pixels(8, 8)]
        void sum(ItemInput i, Canvas canvas)
        {
            uint w, h;
            a.GetDimensions(w, h);
            shown[group.pixel] = float4(a[group.pixel] + b[group.pixel] + c[group.pixel] + d[group.pixel], e[group.pixel], float(f[group.pixel]));
        }

        void graph() { flat(a, b, c, d, e, f); sum(); present(shown); }
        """;

    [Test]
    public void SixOutputsTakeThreeTargets()
    {
        DrawLayout layout = DrawLayout.Of([
            new("a", "float"), new("b", "float"), new("c", "float"), new("d", "float"), new("e", "float2"), new("f", "uint"),
        ]);

        Assert.That(layout.Targets, Is.EqualTo(new[] { "float4", "float2", "uint" }));
        Assert.That(layout.Slots.Select(s => (s.Target, s.Channel)), Is.EqualTo(new[] { (0, 0), (0, 1), (0, 2), (0, 3), (1, 0), (2, 0) }));
    }

    [Test]
    public void FourOutputsKeepATargetEach()
    {
        DrawLayout layout = DrawLayout.Of([new("a", "float"), new("b", "float"), new("c", "uint"), new("d", "float2")]);

        Assert.That(layout.Packs, Is.False);
        Assert.That(layout.Targets, Is.EqualTo(new[] { "float", "float", "uint", "float2" }));
    }

    [Test]
    public void TheFourFloatsBecomeOneTextureClearedChannelByChannel()
    {
        CompiledShader compiled = Compile(Six);
        StackBuild build = compiled.Build!.Shape;

        Assert.That(build.Packs, Has.Count.EqualTo(1));
        BuildImage texture = build.Images[build.Packs[0].Texture];
        Assert.That(texture.Name, Is.EqualTo("a+b+c+d"));
        Assert.That(texture.Format, Is.EqualTo("rgba32f"));
        Assert.That(texture.ClearTexel, Is.EqualTo(new System.Numerics.Vector4(0, 1, 0, 0)));
        Assert.That(build.Images.Select(i => i.Name), Is.EqualTo(new[] { "a+b+c+d", "e", "f", "shown" }));
    }

    [Test]
    public void ThePackedDrawNamesThreeTargets()
    {
        CompiledShader compiled = Compile(Six);
        GraphNode draw = GraphPlan.Of(compiled).Nodes.First(n => n.Kind == NodeKind.Draw && n.Place != NodePlace.Exit);

        Assert.That(Enumerable.Range(0, 4).Select(k => draw.Call.Target(k)), Is.EqualTo(new uint[] { 0, 1, 2, StackBuild.Run.None }));
    }

    [Test]
    public void APackedImagePassedOnWholeIsAProblem()
    {
        string passedOn = Six.Replace("[Pixels(8, 8)]", "float first(RWTexture2D<float> t) { return t[uint2(0)]; }\n[Pixels(8, 8)]")
            .Replace("shown[group.pixel] = float4(", "shown[group.pixel] = first(a) + float4(");

        Assert.That(() => Compile(passedOn), Throws.Exception.With.Message.Contains("can't be passed on whole"));
    }
}
