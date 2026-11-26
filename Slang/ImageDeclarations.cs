using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A graph's image declarations read (<see cref="BuildDeclarations"/>): its texel format, clear, life and size, or a history of another.</summary>
internal static class ImageDeclarations
{
    public static BuildImage Read(Match match, Dictionary<string, DeclaredArgument?> said, System.Func<string, BuildValue?> constant)
    {
        string name = match.Groups["name"].Value, type = match.Groups["type"].Value;
        string? precision = said.ContainsKey("Precise") ? "Precise" : said.ContainsKey("Compact") || said.ContainsKey("View") ? "Compact" : null;
        string format = Texel.Of(type, precision)?.Format ?? throw new BuildProblem(match.Index, Texel.Of(type, null) is null
            ? $"an image holds {string.Join(", ", Texel.Types)}; not {type}."
            : $"an Image<{type}> can't be [{precision}]: that's for {string.Join(", ", Texel.All.Where(texel => texel.Precision == precision).Select(texel => texel.Type))}.");
        float clear = said.TryGetValue("Clear", out DeclaredArgument? value) && value is not null
            ? (float)BuildPasses.Evaluate(BuildPasses.ParseAt(value.Value, value.Index), value.Index, constant).Number
            : 0f;
        ImageLife life = said.ContainsKey("Kept") ? ImageLife.Kept : said.ContainsKey("Cached") ? ImageLife.Cached : ImageLife.Frame;
        return new BuildImage(name, format, type, clear, match.Index, match.Index + match.Length, life,
            Size: SizeOf(match, said, name, constant), Shared: said.ContainsKey("Shared"), View: said.ContainsKey("View"));
    }

    public static BuildImage ReadHistory(Match match, Dictionary<string, DeclaredArgument?> said, List<BuildImage> images, int sources)
    {
        string name = match.Groups["name"].Value, type = match.Groups["type"].Value;
        if (said.Keys.Any(word => word != "History"))
            throw new BuildProblem(match.Index, $"a history is its source's copy, so it takes nothing but [History(image)]: [History(image)] Image<T> {name};.");
        string from = said["History"]?.Value.Trim() ?? "";
        int source = images.FindIndex(0, sources, image => image.Name == from);
        if (source < 0)
            throw new BuildProblem(match.Index, $"{name} is the history of {from}, which isn't an Image this graph declares.");
        BuildImage of = images[source];
        if (of.Screen)
            throw new BuildProblem(match.Index, $"{from} is [{(of.View ? "View" : "Shared")}], the frame's, so it keeps no history.");
        if (of.Life is ImageLife.Kept or ImageLife.Cached)
            throw new BuildProblem(match.Index, $"{from} is [{of.Life}]: it already holds last frame's texels, so it needs no history.");
        if (images.Any(image => image.Source == source))
            throw new BuildProblem(match.Index, $"{from} has a history already.");
        if (type != of.Type)
            throw new BuildProblem(match.Index, $"{from} holds {of.Type}, so its history does too: [History({from})] Image<{of.Type}> {name};.");
        return new BuildImage(name, of.Format, of.Type, of.Clear, match.Index, match.Index + match.Length, ImageLife.History, source, of.Size);
    }

    /// <summary>An image's size as its declaration gives it: a scale of the box, a fixed size, or neither.</summary>
    private static ImageSize SizeOf(Match match, Dictionary<string, DeclaredArgument?> said, string name, System.Func<string, BuildValue?> constant)
    {
        double Number(string text, int at) => BuildPasses.Evaluate(BuildPasses.ParseAt(text, at), at, constant).Number;
        said.TryGetValue("Scale", out DeclaredArgument? scaled);
        said.TryGetValue("Size", out DeclaredArgument? sized);
        if (scaled is not null && sized is not null)
            throw new BuildProblem(match.Index, $"{name} has a scale or a size, not both.");
        if (scaled is not null)
        {
            double scale = Number(scaled.Value, scaled.Index);
            if (scale is <= 0 or > 4)
                throw new BuildProblem(scaled.Index, $"a scale is above 0 and at most 4 ({name} has {scale}).");
            return new ImageSize((float)scale);
        }

        if (sized is null)
            return default;
        string[] sides = sized.Value.Split(',');
        if (sides.Length != 2)
            throw new BuildProblem(sized.Index, $"an image's size is its width and height: [Size(256, 256)] Image<T> {name};.");
        double width = Number(sides[0], sized.Index), height = Number(sides[1], sized.Index + sides[0].Length + 1);
        if (width != System.Math.Floor(width) || height != System.Math.Floor(height) || width is < 1 or > 8192 || height is < 1 or > 8192)
            throw new BuildProblem(sized.Index, $"a fixed size is whole texels, 1 to 8192 each way ({name} is {width} × {height}).");
        return new ImageSize(0, (int)width, (int)height);
    }
}
