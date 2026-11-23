using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>
/// The library precompiled to modules (<see cref="SlangModules"/>) builds, and every shipped
/// effect compiles against it to the same SPIR-V as from the sources it was built from. Slang has
/// read some declarations back wrong from modules before; this is where that shows.
/// </summary>
[TestFixture]
public sealed class PrecompiledLibraryTests
{
    private string _cache = "";

    [SetUp]
    public void UseAFreshCache()
    {
        ShippedLibrary.UseForCompiling();
        _cache = Path.Combine(Path.GetTempPath(), "xanadu-modules-test-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void DropTheCache()
    {
        SlangShaders.CacheFolder = null;
        SlangShaders.LibraryPath = ShippedLibrary.Path;
        if (Directory.Exists(_cache))
            Directory.Delete(_cache, recursive: true);
    }

    private static string[] Effects() =>
        Directory.EnumerateFiles(Path.GetDirectoryName(ShippedLibrary.Path)!, "*.slang", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Library{Path.DirectorySeparatorChar}"))
            .Order()
            .ToArray();

    [Test]
    public void EveryEffectCompilesTheSameAgainstTheModules()
    {
        string[] effects = Effects();
        SlangShaders.CacheFolder = _cache;
        byte[][] precompiled = effects.Select(path => SlangShaders.CompileEffectBody(path, File.ReadAllText(path), SlangShaders.IsLabOnly(path)).Spirv).ToArray();
        Assert.That(SlangShaders.ModuleProblem, Is.Null);
        Assert.That(Directory.EnumerateFiles(_cache, "*.slang-module", SearchOption.AllDirectories), Is.Not.Empty);

        // Against the sources as the modules were built from them: the library with its vector constants as getters.
        string copy = Path.Combine(_cache, "source");
        foreach (string file in Directory.EnumerateFiles(ShippedLibrary.Path, "*.slang", SearchOption.AllDirectories))
        {
            string target = Path.Combine(copy, Path.GetRelativePath(ShippedLibrary.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, SlangModules.AsGetters(File.ReadAllText(file)));
        }

        SlangShaders.CacheFolder = null;
        SlangShaders.LibraryPath = copy;
        for (int i = 0; i < effects.Length; i++)
        {
            byte[] fromSource = SlangShaders.CompileEffectBody(effects[i], File.ReadAllText(effects[i]), SlangShaders.IsLabOnly(effects[i])).Spirv;
            Assert.That(precompiled[i], Is.EqualTo(fromSource), Path.GetFileName(effects[i]));
        }
    }

    private string[] Stored(string module) =>
        Directory.EnumerateFiles(Path.Combine(_cache, "modules", "store"), module + "-*.slang-module").ToArray();

    [Test]
    public void AnEditBuildsOnlyTheModuleAndWhatImportsIt()
    {
        string library = Path.Combine(_cache, "library");
        foreach (string file in Directory.EnumerateFiles(ShippedLibrary.Path, "*.slang", SearchOption.AllDirectories))
        {
            string target = Path.Combine(library, Path.GetRelativePath(ShippedLibrary.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        SlangShaders.LibraryPath = library;
        SlangShaders.CacheFolder = _cache;
        const string body = "import xanadu;\nfloat4 shade(Canvas canvas) { return colours.gold * Circle(40).filled(); }";
        byte[] before = SlangShaders.CompileEffectBody("test", body).Spirv;

        string paint = Path.Combine(library, "xanadu", "paint.slang");
        Assert.That(SlangShaders.WriteLibraryModule(paint, File.ReadAllText(paint) + "\n// edited\n", out string? error), Is.True, error);
        byte[] after = SlangShaders.CompileEffectBody("test", body).Spirv;

        Assert.That(SlangShaders.ModuleProblem, Is.Null);
        Assert.That(after, Is.EqualTo(before));
        // paint, and the umbrella module that imports it; nothing paint imports.
        Assert.That(Stored("xanadu.paint"), Has.Length.EqualTo(2));
        Assert.That(Stored("xanadu"), Has.Length.EqualTo(2));
        foreach (string module in new[] { "xanadu.stage", "xanadu.math", "xanadu.shape", "xanadu.gradient", "xanadu.anim" })
            Assert.That(Stored(module), Has.Length.EqualTo(1), module);
    }

    [Test]
    public void APlainModulesEntryIsKeptInTheCache()
    {
        SlangShaders.CacheFolder = _cache;
        Directory.CreateDirectory(_cache);
        string module = Path.Combine(_cache, "plain.slang");
        File.WriteAllText(module, "[shader(\"fragment\")] float4 tint(float2 uv : TEXCOORD0) : SV_Target { return float4(uv, 0, 1); }");

        byte[] compiled = SlangShaders.Compile(module, "tint", "fragment");
        string[] kept = Directory.GetFiles(_cache, "*.spv");
        Assert.That(kept, Has.Length.EqualTo(1));

        // Read back, not compiled again: what the cache holds is what comes out.
        File.WriteAllBytes(kept[0], [.. compiled, 0, 0, 0, 0]);
        Assert.That(SlangShaders.Compile(module, "tint", "fragment"), Has.Length.EqualTo(compiled.Length + 4));

        File.WriteAllText(module, File.ReadAllText(module) + "\n// changed");
        Assert.That(SlangShaders.Compile(module, "tint", "fragment"), Is.EqualTo(compiled));
    }

    [Test]
    public void StoredVectorConstantsBecomeGettersOnTheSameLine()
    {
        string source = "public static const colour gold = float4(1.0, 0.84, 0.0, 1.0);\npublic static const float PI = 3.14;";

        string built = SlangModules.AsGetters(source);

        Assert.That(built, Is.EqualTo("public property colour gold { get { return float4(1.0, 0.84, 0.0, 1.0); } }\npublic static const float PI = 3.14;"));
    }
}
