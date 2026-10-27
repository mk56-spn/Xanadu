using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xanadu.Animation;

/// <summary>SVG group isolation used before rasterizing a rigid attachment.</summary>
public static class XafSvgDocument
{
    /// <summary>
    /// Ids of the groups an SVG can be attached as, in document order. Only
    /// <c>&lt;g&gt;</c> elements outside <c>defs</c> count, since those are what
    /// an attachment isolates and rasterizes; their ids can then be passed to
    /// <see cref="IsolateGroup"/>.
    /// </summary>
    public static List<string> Groups(string svg)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception)
        {
            throw new XafFormatException(
                $"Invalid SVG: {exception.Message}");
        }

        var groups = new List<string>();
        if (document.Root is null)
            return groups;

        foreach (XElement element in document.Root.Descendants())
        {
            if (element.Name.LocalName != "g" ||
                element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "defs"))
            {
                continue;
            }

            string? id = (string?)element.Attribute("id");
            if (!string.IsNullOrWhiteSpace(id) && !groups.Contains(id))
                groups.Add(id);
        }

        return groups;
    }

    public static string IsolateGroup(string svg, string group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return svg;

        XDocument document;
        try
        {
            document = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception)
        {
            throw new XafFormatException(
                $"Invalid SVG: {exception.Message}");
        }

        XElement? root = document.Root;
        if (root is null)
            throw new XafFormatException("SVG has no root element.");

        XElement? target = root.Descendants()
            .FirstOrDefault(element =>
                string.Equals(
                    (string?)element.Attribute("id"),
                    group,
                    StringComparison.Ordinal));
        if (target is null)
            throw new XafFormatException($"SVG group '{group}' was not found.");

        var isolated = new XElement(root.Name, root.Attributes());
        foreach (XElement child in root.Elements())
        {
            if (child.Name.LocalName is "defs" or "style")
                isolated.Add(new XElement(child));
        }

        var chain = target.AncestorsAndSelf()
            .Reverse()
            .Where(element => element != root)
            .ToList();
        XElement? branch = null;
        foreach (XElement element in chain)
        {
            var copy = new XElement(element.Name, element.Attributes());
            if (element == target)
            {
                foreach (XNode node in element.Nodes())
                    copy.Add(node);
            }

            if (branch is null)
                isolated.Add(copy);
            else
                branch.Add(copy);
            branch = copy;
        }

        return isolated.ToString(SaveOptions.DisableFormatting);
    }
}
