using NUnit.Framework;
using Xanadu.Levels;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class CommonShadersTests
{
    [Test]
    public void KnowsItsOwnPathsAndNothingElse()
    {
        Assert.That(CommonShaders.IsKnown(CommonShaders.Paths[0]), Is.True);
        Assert.That(CommonShaders.IsKnown("not-a-real-shader.gdshader"), Is.False);
        Assert.That(CommonShaders.IsKnown(""), Is.False);
    }
}
