using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Rendering;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.Shaders;

/// <summary>The instance-uniform declarations a group's shader track may key.</summary>
[TestFixture]
public sealed class ShaderSourceUniformTests
{
    [Test]
    public void OnlyInstanceTaggedUniformsAreKeyable()
    {
        const string source = """
            [Instance] uniform float speed;
            uniform float tuned;
            [Instance] uniform float4 tint;
            [Instance]
            uniform float2 wobble;
            float4 shade(ItemInput i) { return i.colour; }
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
            [Instance] [Default(1.0)] uniform float strength;
            [Default(1.0, 0.5, 0.25, 1.0)] [Instance] uniform float4 tint;
            [Instance][Default(2)] uniform float2 scale;
            [Instance] uniform float plain;
            [Instance] [Default(sin(1))] uniform float odd;
            """;

        List<InstanceUniform> uniforms = ShaderSourceUniforms.InstanceUniforms(source);

        Assert.That(uniforms.Select(u => u.Name).ToArray(), Is.EqualTo(new[] { "strength", "tint", "scale", "plain", "odd" }));
        Assert.That(uniforms[0].Default, Is.EqualTo(new Vector4(1f, 0, 0, 0)));
        Assert.That(uniforms[1].Default, Is.EqualTo(new Vector4(1f, 0.5f, 0.25f, 1f)), "attributes may come in either order");
        Assert.That(uniforms[2].Default, Is.EqualTo(new Vector4(2f, 0, 0, 0)), "missing channels are zero, as Slang reflects them");
        Assert.That(uniforms[3].Default, Is.EqualTo(Vector4.Zero));
        Assert.That(uniforms[4].Default, Is.EqualTo(Vector4.Zero), "an expression isn't evaluated");
    }

    [Test]
    public void CommentsAreSkipped()
    {
        const string source = """
            [Instance] uniform float speed;
            // [Instance] uniform float commented;
            /* [Instance] uniform float blocked; */
            [Instance] uniform float3 glow;
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
