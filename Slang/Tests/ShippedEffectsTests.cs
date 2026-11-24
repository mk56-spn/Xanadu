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

    /// <summary>A stack compiled as the host compiles it: its bricks composed, lab-only ones allowed to keep state and pools.</summary>
    internal static CompiledShader Compile(string folder) =>
        SlangShaders.CompileEffectBody(folder, ShaderStack.Compose(folder, ShaderStack.Read(folder)), SlangShaders.IsLabOnly(folder + "/"));

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
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/note_background"), Is.False);
    }

    /// <summary>Kept state anywhere else is still refused, so a level never gets it by accident.</summary>
    [Test]
    public void KeptStateOutsideTheLabFolderIsRefused()
    {
        string tiled = Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Lab", "raymarch_tiled");
        string body = ShaderStack.Compose("Assets/Shaders/Misc/x", ShaderStack.Read(tiled));
        Assert.Throws<System.InvalidOperationException>(() => SlangShaders.CompileEffectBody("Assets/Shaders/Misc/x", body));
    }

}
