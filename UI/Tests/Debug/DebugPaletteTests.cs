using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Xanadu.Ui.Tests.Debug;

/// <summary>Keeps colours from creeping back into the debug views one at a time.</summary>
[TestFixture]
public sealed class DebugPaletteTests
{
    private static readonly string[] Views =
    {
        "UI/Systems/Debug/DebugPressedOverlaySystem.cs",
        "UI/Systems/Debug/DebugTable.cs",
        "UI/Systems/Debug/FpsCounterSystem.cs",
        "UI/Systems/Debug/DebugPressedOverlaySystem.Archetypes.cs",
        "UI/Systems/Ui/DebugElementOverlaySystem.cs"
    };

    private static readonly Regex Hardcoded = new(
        @"new Color\((?!value\[|defaults\[|0, 0, 0, 0\))|new\(""#|\bColors\.\w|(?<!Debug)Palette\.\w");

    [TestCaseSource(nameof(Views))]
    public void TheViewTakesItsColoursFromTheDebugPalette(string view)
    {
        string[] offending = File.ReadAllLines(Path.Combine(RepositoryRoot(), view))
            .Where(line => Hardcoded.IsMatch(line)).ToArray();

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
