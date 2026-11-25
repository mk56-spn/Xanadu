using System.IO;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Every stack the game ships (Assets/Shaders, outside the library) compiles as the host builds it: its bricks composed, through the level shader wrapper.</summary>
[TestFixture]
public sealed class ShippedEffectsTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    /// <summary>Every shipped stack's folder.</summary>
    internal static string[] Stacks() =>
        [.. ShaderStack.Under(Path.GetDirectoryName(ShippedLibrary.Path)!, skip: ShippedLibrary.Path)];

    /// <summary>A stack compiled as the host compiles it: its files composed.</summary>
    internal static CompiledShader Compile(string folder) =>
        SlangShaders.CompileEffectBody(folder, ShaderStack.Compose(folder, ShaderStack.Read(folder)));

    [TestCaseSource(nameof(Stacks))]
    public void Compiles(string folder)
    {
        CompiledShader compiled = Compile(folder);

        Assert.That(compiled.Spirv.Length, Is.GreaterThan(20));
    }

    [Test]
    public void OnlyTheLabFolderIsLabOnly()
    {
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/Lab/raymarch_tiled"), Is.True);
        Assert.That(SlangShaders.IsLabOnly("/home/x/game/Assets/Shaders/Lab/a"), Is.True);
        Assert.That(SlangShaders.IsLabOnly(@"C:\game\Assets\Shaders\Lab\a.slang"), Is.True);
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/Misc/Lab"), Is.False);
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/Game/note_background"), Is.False);
    }
}
