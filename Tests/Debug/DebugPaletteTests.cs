using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Xanadu.Tests.Debug;

/// <summary>Keeps colours from creeping back into the debug views one at a time.</summary>
[TestFixture]
public sealed class DebugPaletteTests
{
    private static readonly string[] Views =
    {
        "Systems/Debug/DebugPressedOverlaySystem.cs",
        "Systems/Debug/DebugTable.cs",
        "Systems/Debug/FpsCounterSystem.cs",
        "Systems/Debug/DebugPressedOverlaySystem.Archetypes.cs",
        "Systems/Playfields/PlayfieldRendererHitboxes.cs",
        "Systems/Playfields/PlayfieldHitboxOverlaySystem.cs",
        "Systems/Shaders/ShaderTunerSystem.cs",
        "UI/Systems/Ui/DebugElementOverlaySystem.cs",
    };

    // The tuner shows the colour being tuned, which is data rather than styling.
    private static readonly Regex Hardcoded = new(
        @"new Color\((?!value\[|defaults\[|0, 0, 0, 0\))|new\(""#|\bColors\.\w|(?<!Debug)Palette\.\w");

    [TestCaseSource(nameof(Views))]
    public void TheViewTakesItsColoursFromTheDebugPalette(string view)
    {
        string[] offending = File.ReadAllLines(Path.Combine(RepositoryRoot(), view))
            .Where(line => Hardcoded.IsMatch(line))
            .ToArray();

        Assert.That(offending, Is.Empty);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "agents.md")))
            directory = directory.Parent;
        return directory.FullName;
    }
}
