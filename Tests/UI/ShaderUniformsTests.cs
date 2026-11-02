using NUnit.Framework;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class ShaderUniformsTests
{
    [Test]
    public void GuessedRangesBracketTheCurrentValue()
    {
        (float min, float max) = ShaderUniforms.GuessRange(UniformKind.Float, 250);
        Assert.That((min, max), Is.EqualTo((0f, 1000f)));

        (min, max) = ShaderUniforms.GuessRange(UniformKind.Float, 0.4f);
        Assert.That(max, Is.EqualTo(1.6f).Within(1e-5f));

        (min, max) = ShaderUniforms.GuessRange(UniformKind.Float, -3);
        Assert.That(min, Is.LessThan(-3));

        Assert.That(ShaderUniforms.GuessRange(UniformKind.Color, 0.5f), Is.EqualTo((0f, 1f)));
    }
}
