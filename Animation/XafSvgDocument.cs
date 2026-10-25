using System;
using System.Linq;
using System.Xml.Linq;

namespace Writer.Animation;

/// <summary>SVG group isolation used before rasterizing a rigid attachment.</summary>
public static class XafSvgDocument
{
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
