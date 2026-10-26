using System.Collections.Generic;

namespace Writer.Tools.Animator.Files;

/// <summary>
/// The SVG art a rig can attach: every <c>.svg</c> file the project holds and
/// the group ids inside each, which is what an attachment names.
/// </summary>
public interface IAnimatorSvgCatalog
{
    /// <summary>SVG files under the project's asset folder, as project paths, sorted.</summary>
    IReadOnlyList<string> Files();

    /// <summary>The attachable groups of one SVG file; empty when it cannot be read.</summary>
    IReadOnlyList<string> Groups(string svgPath);
}
