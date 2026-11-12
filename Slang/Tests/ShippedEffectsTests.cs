using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Every effect the game ships (Assets/Shaders, outside the library) compiles as the host builds it.</summary>
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
        CompiledShader compiled = SlangShaders.CompileReflected(path, "shade", "fragment", samplerSpace: 2, uniformSpace: 3);

        Assert.That(compiled.Spirv.Length, Is.GreaterThan(20));
    }
}
