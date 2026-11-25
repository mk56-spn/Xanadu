using NUnit.Framework;
using Xanadu.Model.Levels;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class CommonShadersTests
{
    [Test]
    public void AnyStackUnderTheShadersFolderIsKnown()
    {
        Assert.That(CommonShaders.IsKnown(CommonShaders.Paths[0]), Is.True);
        Assert.That(CommonShaders.IsKnown("Assets/Shaders/Lab/scene_room"), Is.True);
        Assert.That(CommonShaders.IsKnown("Assets/Shaders/Learn/planet"), Is.True);
    }

    [Test]
    public void TheLibraryAndPathsOutsideAreNot()
    {
        Assert.That(CommonShaders.IsKnown("Assets/Shaders/Library/xanadu"), Is.False);
        Assert.That(CommonShaders.IsKnown("Assets/Shaders/"), Is.False);
        Assert.That(CommonShaders.IsKnown("Assets/Shaders/../Songs"), Is.False);
        Assert.That(CommonShaders.IsKnown("not-a-real-shader.gdshader"), Is.False);
        Assert.That(CommonShaders.IsKnown(""), Is.False);
    }
}
