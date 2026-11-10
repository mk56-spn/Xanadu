using System.IO;
using System.Linq;
using NUnit.Framework;
using Xanadu.Rendering;
using Xanadu.Storage;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class ShaderConstantsTests
{
    private const string Library = """
        module lib;
        public static const float PI = 3.5;
        public namespace color
        {
            public static const float4 gold = float4(1.0, 0.84, 0.0, 1.0);
            public static const float4 grey = float4(0.5);
            public float3 hue(float h) { return float3(h); }
        }
        public static const float4 white = float4(1, 1, 1, 1);
        """;

    [Test]
    public void ConstantsAreNamedAsCodeNamesThem()
    {
        ShaderConstants constants = ShaderConstants.FromSources([Library]);

        Assert.That(constants.TryGet("PI", out float[] pi) && pi.SequenceEqual([3.5f]), Is.True);
        Assert.That(constants.TryGet("color.gold", out float[] gold) && gold.SequenceEqual([1f, 0.84f, 0f, 1f]), Is.True);
        Assert.That(constants.TryGet("color::gold", out _), Is.True);
        Assert.That(constants.TryGet("color.grey", out float[] grey) && grey.SequenceEqual([0.5f, 0.5f, 0.5f, 0.5f]), Is.True);
        Assert.That(constants.TryGet("white", out _), Is.True);
        Assert.That(constants.TryGet("gold", out _), Is.False);
    }

    [Test]
    public void DefaultsThatNameAConstantBecomeItsNumbers()
    {
        ShaderConstants constants = ShaderConstants.FromSources([Library]);

        string expanded = constants.ExpandDefaults("[Default(color.gold)] uniform float4 tint;\n[Default(0.5)] uniform float speed;");

        Assert.That(expanded, Is.EqualTo("[Default(1, 0.84, 0, 1)] uniform float4 tint;\n[Default(0.5)] uniform float speed;"));
        Assert.That(constants.ExpandDefaults("[Default(PI, 2)] uniform float2 v;"), Is.EqualTo("[Default(3.5, 2)] uniform float2 v;"));
        Assert.That(constants.ExpandDefaults("[Default(nothing)] uniform float v;"), Is.EqualTo("[Default(nothing)] uniform float v;"));
    }

    [Test]
    public void InstanceUniformsReadNamedDefaults()
    {
        ShaderConstants.Use(ShaderConstants.FromSources([Library]));
        try
        {
            InstanceUniform tint = ShaderSourceUniforms.InstanceUniforms("[Instance] [Default(color.gold)] uniform float4 tint;").Single();
            Assert.That(tint.Default, Is.EqualTo(new Vector4(1f, 0.84f, 0f, 1f)));
        }
        finally
        {
            ShaderConstants.Use(new ShaderConstants());
        }
    }

    [Test]
    public void TheShippedLibraryNamesItsColours()
    {
        string folder = GamePaths.Globalize("res://Assets/Shaders/Library");
        ShaderConstants constants = ShaderConstants.FromSources(Directory.EnumerateFiles(folder, "*.slang", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.That(constants.TryGet("color.gold", out float[] gold) && gold.SequenceEqual([1f, 0.84f, 0f, 1f]), Is.True);
        Assert.That(constants.TryGet("color.midnightBlue", out _), Is.True);
        Assert.That(constants.TryGet("TAU", out _), Is.True);
    }
}
