using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Every effect the game ships (Assets/Shaders, outside the library) compiles as the host builds it: through the level shader wrapper.</summary>
[TestFixture]
public sealed class ShippedEffectsTests
{
    [OneTimeSetUp]
    public void UseTheLibrary() => ShippedLibrary.UseForCompiling();

    private static string[] Effects() =>
        Directory.EnumerateFiles(Path.GetDirectoryName(ShippedLibrary.Path)!, "*.slang", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}"))
            .Order()
            .ToArray();

    [TestCaseSource(nameof(Effects))]
    public void Compiles(string path)
    {
        // Lab-only effects may keep state; the game never loads them.
        CompiledShader compiled = SlangShaders.CompileEffectBody(path, File.ReadAllText(path), SlangShaders.IsLabOnly(path));

        Assert.That(compiled.Spirv.Length, Is.GreaterThan(20));
    }

    [Test]
    public void OnlyTheLabFolderIsLabOnly()
    {
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/Lab/raymarch_tiled.slang"), Is.True);
        Assert.That(SlangShaders.IsLabOnly("/home/x/game/Assets/Shaders/Lab/a.slang"), Is.True);
        Assert.That(SlangShaders.IsLabOnly(@"C:\game\Assets\Shaders\Lab\a.slang"), Is.True);
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/Misc/Lab.slang"), Is.False);
        Assert.That(SlangShaders.IsLabOnly("Assets/Shaders/note_background.slang"), Is.False);
    }

    /// <summary>Kept state anywhere else is still refused, so a level never gets it by accident.</summary>
    [Test]
    public void KeptStateOutsideTheLabFolderIsRefused()
    {
        string tiled = Path.Combine(Path.GetDirectoryName(ShippedLibrary.Path)!, "Lab", "raymarch_tiled.slang");
        Assert.Throws<System.InvalidOperationException>(() => SlangShaders.CompileEffectBody("Assets/Shaders/Misc/x.slang", File.ReadAllText(tiled)));
    }

}
