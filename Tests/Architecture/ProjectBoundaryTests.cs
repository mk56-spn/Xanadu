using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Writer.Tests.Architecture;

[TestFixture]
public sealed class ProjectBoundaryTests
{
    private static string Root => Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "../../../../"));

    [Test]
    public void GenericUiDoesNotReferenceGameNamespaces()
    {
        string[] forbidden =
        {
            "Writer.Scenes",
            "Writer.Editor",
            "Writer.Levels",
            "Writer.Settings",
            "Writer.Profiles",
        };

        foreach (string file in Directory.GetFiles(
                     Path.Combine(Root, "UI"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (string name in forbidden)
            {
                Assert.That(
                    source.Contains($"using {name}"),
                    Is.False,
                    $"{file} references {name}");
            }
        }
    }

    [Test]
    public void CoreDoesNotReferenceUiOrGame()
    {
        foreach (string file in Directory.GetFiles(
                     Path.Combine(Root, "Core"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Assert.That(source.Contains("using Writer.Ui"), Is.False, file);
            Assert.That(source.Contains("using Writer.Scenes"), Is.False, file);
        }
    }

    [Test]
    public void EditorItemsAreCreatedThroughTheFactory()
    {
        string[] roots =
        {
            Path.Combine(Root, "Systems", "Editor"),
            Path.Combine(Root, "Editor"),
            Path.Combine(Root, "Levels"),
        };
        string[] direct =
        {
            "CreateEntity(new Note(",
            "CreateEntity(new Mine(",
            "CreateEntity(new StartPoint(",
        };

        foreach (string root in roots)
        {
            foreach (string file in Directory.GetFiles(
                         root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                foreach (string call in direct)
                {
                    Assert.That(
                        source.Contains(call),
                        Is.False,
                        $"{file} bypasses EditorItems.Create via {call}");
                }
            }
        }
    }
}
