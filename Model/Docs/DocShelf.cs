using System.Collections.Generic;

namespace Xanadu.Model.Docs;

/// <summary>The built-in docs' sections, in the order the side column lists them.</summary>
public enum DocSection
{
    Shaders,
    Songs,
    Ui,
    Particles,
    Levels,
    Editor,
}

/// <summary>
/// One page of the built-in docs: a section's title over its body. The body
/// wraps to the page; a newline starts a new line, two leave a gap.
/// </summary>
public sealed record DocPage(DocSection Section, string Name, string Title, string Body);

/// <summary>
/// Every page the built-in docs hold, one per section for now. The content is
/// placeholder; real pages will come from files through Storage, and this
/// shelf is what they will fill.
/// </summary>
public static class DocShelf
{
    private const string Paragraph =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure " +
        "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. " +
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt " +
        "mollit anim id est laborum.";

    private const string Lorem = Paragraph + "\n\n" + Paragraph + "\n\n" + Paragraph;

    public static IReadOnlyList<DocPage> Pages { get; } =
    [
        new(DocSection.Shaders, "SHADERS", "Shaders", Lorem),
        new(DocSection.Songs, "SONGS", "Songs", Lorem),
        new(DocSection.Ui, "UI", "UI", Lorem),
        new(DocSection.Particles, "PARTICLES", "Particles", Lorem),
        new(DocSection.Levels, "LEVELS", "Levels", Lorem),
        new(DocSection.Editor, "EDITOR", "Editor", Lorem),
    ];
}
