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
        CompiledShader compiled = SlangShaders.CompileEffectBody(path, File.ReadAllText(path));

        Assert.That(compiled.Spirv.Length, Is.GreaterThan(20));
    }

    /// <summary>The game sets values and textures by name, so the wrapper's namespace mustn't show in them.</summary>
    [Test]
    public void ValuesAndTexturesKeepTheirPlainNames()
    {
        string folder = Path.GetDirectoryName(ShippedLibrary.Path)!;
        string background = Path.Combine(folder, "note_background.slang");
        CompiledShader notes = SlangShaders.CompileEffectBody(background, File.ReadAllText(background));
        string flarePath = Path.Combine(folder, "flare.slang");
        CompiledShader flare = SlangShaders.CompileEffectBody(flarePath, File.ReadAllText(flarePath));

        Assert.That(notes.Samplers.ElementAtOrDefault(2), Is.EqualTo("notes"));
        Assert.That(notes.Params.Select(p => p.Name), Does.Contain("glyph_count").And.Contain("energy"));
        Assert.That(flare.Params.Select(p => p.Name), Is.EquivalentTo(new[] { "flare_box", "flare_light", "flare_tint", "flare_shape" }));
    }
}
