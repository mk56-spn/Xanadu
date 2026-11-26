using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>What a handle on the preview does to the value it is bound to.</summary>
public enum HandleKind
{
    /// <summary>A <c>float</c> distance from the centre (a circle's radius): a dot on the shape's right, dragged out or in.</summary>
    Radius,
    /// <summary>A <c>float2</c> whole width and height (a box's size): grips on the right edge, the bottom edge and the corner.</summary>
    Size,
    /// <summary>A <c>float2</c> half width and height (an ellipse's radii): the same grips, the value half as big.</summary>
    Half,
    /// <summary>A <c>float</c> corner rounding: a dot on the top edge, dragged in from the corner.</summary>
    Corner,
    /// <summary>A <c>float2</c> place in the shape's own space (a line's ends): a dot at it.</summary>
    Point,
    /// <summary>A placement's <c>float2</c> offset: the shape's body and a square at its centre.</summary>
    Move,
    /// <summary>A placement's <c>float</c> angle in radians: a dot at the end of an arm, swung round the centre.</summary>
    Turn,
    /// <summary>A placement's <c>float</c> scale factor: a square out on the diagonal, pulled away from the centre or in.</summary>
    Scale,
}

/// <summary>One <c>/// @handle parameter kind</c> tag: a constructor's or a method's parameter, and what its handle does.</summary>
public readonly record struct HandleTag(string Parameter, HandleKind Kind);

/// <summary>
/// The library's handle tags. A shape struct tags its constructor's parameters
/// (<c>/// @handle radius radius</c> on <c>Circle</c>), a placement method its own
/// (<c>/// @handle offset move</c> on <c>moved</c>), in the doc comment over the
/// declaration as <c>@cost</c> is. Which structs are shapes (implement <c>IShape</c>)
/// comes along: every shape can be moved, turned and scaled, tagged or not.
/// </summary>
public sealed partial class SlangHandleTags
{
    private readonly Dictionary<string, List<HandleTag>> _tags = [];
    private readonly HashSet<string> _shapes = [];

    [GeneratedRegex(@"^\s*///\s*@handle\s+(\w+)\s+(\w+)")]
    private static partial Regex Tag();

    [GeneratedRegex(@"^\s*public\s+struct\s+(\w+)\s*:\s*([^{]*)")]
    private static partial Regex Struct();

    [GeneratedRegex(@"^\s*public\s+(?:static\s+)?[\w<>:, ]+?\s+(\w+)\s*(?:<[^>()]*>)?\s*\(")]
    private static partial Regex Method();

    [GeneratedRegex(@"\bIShape\b")]
    private static partial Regex ShapeInterface();

    /// <summary>The tags read from every source given.</summary>
    public static SlangHandleTags FromSources(IEnumerable<string> sources)
    {
        var tags = new SlangHandleTags();
        foreach (string source in sources)
            tags.Read(source);
        return tags;
    }

    /// <summary>Whether a struct of this name is a shape.</summary>
    public bool IsShape(string name) => _shapes.Contains(name);

    /// <summary>The tags on a struct (its constructor's) or a method; empty for one with none.</summary>
    public IReadOnlyList<HandleTag> Of(string name) => _tags.TryGetValue(name, out List<HandleTag>? found) ? found : [];

    /// <summary>The placement method whose tag is <paramref name="kind"/> (<c>moved</c> for Move), or null.</summary>
    public string? MethodFor(HandleKind kind)
    {
        foreach ((string name, List<HandleTag> tags) in _tags)
        {
            if (!_shapes.Contains(name) && tags.Exists(tag => tag.Kind == kind))
                return name;
        }

        return null;
    }

    private void Read(string source)
    {
        List<HandleTag> pending = [];
        foreach (string line in source.Split('\n'))
        {
            Match tag = Tag().Match(line);
            if (tag.Success)
            {
                if (Enum.TryParse(tag.Groups[2].Value, ignoreCase: true, out HandleKind kind))
                    pending.Add(new HandleTag(tag.Groups[1].Value, kind));
                continue;
            }

            string trimmed = line.TrimStart();
            // Doc lines and attributes sit between a tag and what it is on.
            if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith('['))
                continue;

            Match declared = Struct().Match(line);
            if (declared.Success && ShapeInterface().IsMatch(declared.Groups[2].Value))
                _shapes.Add(declared.Groups[1].Value);
            string? name = declared.Success ? declared.Groups[1].Value : Method().Match(line) is { Success: true } method ? method.Groups[1].Value : null;
            if (name is not null && pending.Count > 0)
            {
                if (!_tags.TryGetValue(name, out List<HandleTag>? list))
                    _tags[name] = list = [];
                list.AddRange(pending);
            }

            pending.Clear();
        }
    }
}
