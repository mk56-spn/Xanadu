using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Rendering;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.Shaders;

/// <summary>Step 7 of docs/render-architecture.md: blend variants inject render_mode.</summary>
[TestFixture]
public sealed class ShaderBlendTests
{
    private const string Plain = "shader_type canvas_item;\n\nvoid fragment() { COLOR = vec4(1.0); }\n";

    [Test]
    public void TheBlendLineGoesRightAfterTheShaderType()
    {
        string add = ShaderBlendVariants.Inject(Plain, BlendMode.Add);

        Assert.That(add, Does.Contain("shader_type canvas_item;\nrender_mode blend_add;"));
        Assert.That(add, Does.Contain("void fragment()"));
    }

    [Test]
    public void MixLeavesTheSourceAlone()
    {
        Assert.That(ShaderBlendVariants.Inject(Plain, BlendMode.Mix), Is.EqualTo(Plain));
    }

    [Test]
    public void AnExistingRenderModeKeepsItsOtherModesAndSwapsTheBlend()
    {
        const string source = "shader_type canvas_item;\nrender_mode unshaded, blend_mix;\n";

        string mul = ShaderBlendVariants.Inject(source, BlendMode.Mul);

        Assert.That(mul, Does.Contain("render_mode unshaded, blend_mul;"));
        Assert.That(mul, Does.Not.Contain("blend_mix"));
        Assert.That(mul, Does.Not.Contain("blend_add"));
    }

    [Test]
    public void ARenderModeWithNoBlendGainsOne()
    {
        const string source = "shader_type canvas_item;\nrender_mode unshaded;\n";

        Assert.That(
            ShaderBlendVariants.Inject(source, BlendMode.Premultiplied),
            Does.Contain("render_mode unshaded, blend_premul_alpha;"));
    }

    [Test]
    public void CommentsBeforeTheHeaderAreKept()
    {
        const string source = "// a shader\nshader_type canvas_item;\n";

        string sub = ShaderBlendVariants.Inject(source, BlendMode.Sub);

        Assert.That(sub, Does.StartWith("// a shader\nshader_type canvas_item;\nrender_mode blend_sub;"));
    }

    [Test]
    public void EveryModeHasItsGodotToken()
    {
        Assert.That(ShaderBlendVariants.Token(BlendMode.Mix), Is.Empty);
        Assert.That(ShaderBlendVariants.Token(BlendMode.Add), Is.EqualTo("blend_add"));
        Assert.That(ShaderBlendVariants.Token(BlendMode.Sub), Is.EqualTo("blend_sub"));
        Assert.That(ShaderBlendVariants.Token(BlendMode.Mul), Is.EqualTo("blend_mul"));
        Assert.That(ShaderBlendVariants.Token(BlendMode.Premultiplied), Is.EqualTo("blend_premul_alpha"));
    }

    [Test]
    public void PlainMaterialsMapEveryModeToGodotsBlend()
    {
        Assert.That(PlainBlendMaterials.ToGodot(BlendMode.Mix), Is.EqualTo(Godot.CanvasItemMaterial.BlendModeEnum.Mix));
        Assert.That(PlainBlendMaterials.ToGodot(BlendMode.Add), Is.EqualTo(Godot.CanvasItemMaterial.BlendModeEnum.Add));
        Assert.That(PlainBlendMaterials.ToGodot(BlendMode.Sub), Is.EqualTo(Godot.CanvasItemMaterial.BlendModeEnum.Sub));
        Assert.That(PlainBlendMaterials.ToGodot(BlendMode.Mul), Is.EqualTo(Godot.CanvasItemMaterial.BlendModeEnum.Mul));
        Assert.That(
            PlainBlendMaterials.ToGodot(BlendMode.Premultiplied),
            Is.EqualTo(Godot.CanvasItemMaterial.BlendModeEnum.PremultAlpha));
    }
}

/// <summary>The instance-uniform declarations a group's shader track may key.</summary>
[TestFixture]
public sealed class ShaderSourceUniformTests
{
    [Test]
    public void OnlyInstanceUniformsAreKeyable()
    {
        const string source = """
            shader_type canvas_item;
            instance uniform float speed;
            uniform float tuned;
            instance uniform vec4 tint;
            instance uniform vec2 wobble;
            void fragment() { COLOR = vec4(1.0); }
            """;

        List<InstanceUniform> uniforms = ShaderSourceUniforms.InstanceUniforms(source);

        Assert.That(uniforms.Select(u => u.Name).ToArray(), Is.EqualTo(new[] { "speed", "tint", "wobble" }));
        Assert.That(uniforms.Select(u => u.Channels), Is.EqualTo(new[] { 1, 4, 2 }));
        Assert.That(uniforms, Does.Not.Contain(new InstanceUniform("tuned", 1)));
    }

    [Test]
    public void DeclaredDefaultsAreRead()
    {
        const string source = """
            shader_type canvas_item;
            instance uniform float strength : hint_range(0.0, 2.0, 0.1) = 1.0;
            instance uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.25, 1.0);
            instance uniform vec2 scale = vec2(2.0);
            instance uniform float plain;
            instance uniform float odd = sin(1.0) * 2.0;
            """;

        List<InstanceUniform> uniforms = ShaderSourceUniforms.InstanceUniforms(source);

        Assert.That(uniforms.Select(u => u.Name).ToArray(), Is.EqualTo(new[] { "strength", "tint", "scale", "plain", "odd" }));
        Assert.That(uniforms[0].Default, Is.EqualTo(new Vector4(1f, 0, 0, 0)));
        Assert.That(uniforms[1].Default, Is.EqualTo(new Vector4(1f, 0.5f, 0.25f, 1f)));
        Assert.That(uniforms[2].Default, Is.EqualTo(new Vector4(2f, 2f, 0, 0)), "one value spreads over the vector's channels");
        Assert.That(uniforms[3].Default, Is.EqualTo(Vector4.Zero));
        Assert.That(uniforms[4].Default, Is.EqualTo(Vector4.Zero), "an expression isn't evaluated");
    }

    [Test]
    public void PrecisionQualifiersAndCommentsAreHandled()
    {
        const string source = """
            shader_type canvas_item;
            instance uniform highp float speed;
            // instance uniform float commented;
            instance uniform vec3 glow;
            """;

        Assert.That(
            ShaderSourceUniforms.InstanceUniforms(source).Select(u => u.Name).ToArray(),
            Is.EqualTo(new[] { "speed", "glow" }));
    }

    [Test]
    public void AnEmptySourceHasNoUniforms()
    {
        Assert.That(ShaderSourceUniforms.InstanceUniforms(""), Is.Empty);
        Assert.That(ShaderSourceUniforms.InstanceUniforms(null), Is.Empty);
    }
}
