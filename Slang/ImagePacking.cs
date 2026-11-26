using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>One image packed into a shared texture: its declaration, and the channels it has there.</summary>
public sealed record PackedImage(BuildImage Image, int Channel, int Channels);

/// <summary>Images sharing one texture's channels, so a draw with more than four outputs can write them all.</summary>
/// <param name="Texture">The texture's place in the graph's images, as the runner sees them.</param>
public sealed record ImagePack(int Texture, PackedImage[] Members)
{
    /// <summary>What the texture is called in the code Slang sees.</summary>
    public string Declared => $"xanaduPack{Texture}";
}

/// <summary>
/// Packing a graph's images for its draws (Docs/render-graph.md, stage F): where a draw's layout (<see cref="DrawLayout"/>)
/// puts several outputs in one target, the images its calls name for them become one texture, each the channels its
/// output fills. Every image reaches the runner as a texture of its own or a pack, so the runner never knows: the
/// schedule's draws name packs, and the code reads and writes a packed image through a small struct that picks its
/// channels out (<c>name[pixel]</c>, <c>name.GetDimensions</c>). Images pack only where they can: the same size and life,
/// no history, used by subscript only; anything else stays a texture of its own.
/// </summary>
internal static class ImagePacking
{
    /// <summary>
    /// The graph's images as the runner sees them, the schedule and passes renaming images to those, and the packs.
    /// Unchanged when no draw packs.
    /// </summary>
    public static (List<BuildImage> Images, List<BuildStep> Schedule, List<BuildPass> Passes, List<ImagePack> Packs) Apply(
        string code, List<BuildImage> images, List<BuildPass> passes, List<BuildStep> schedule, (int Start, int End) graph)
    {
        // Each image packed: the pack it is in (by place in `packs`), and its channels there.
        var packedAt = new Dictionary<int, (int Pack, int Channel, int Channels)>();
        var packs = new List<(TexelKind Kind, List<(int Image, int Channel, int Channels)> Members)>();
        List<BuildDraw> draws = [.. Draws(schedule)];
        foreach (BuildDraw draw in draws)
        {
            if (draw.Exits || passes[draw.Pass].Draw is not { } drawn)
                continue;
            DrawLayout layout = DrawLayout.Of(drawn.Outputs, passes[draw.Pass].Start);
            if (!layout.Packs)
                continue;
            for (int t = 0; t < layout.Targets.Count; t++)
            {
                int[] outputs = [.. layout.In(t)];
                if (outputs.Length < 2)
                    continue;
                List<(int Image, int Channel, int Channels)> members =
                    [.. outputs.Select(k => (draw.Targets[k], layout.Slots[k].Channel, drawn.Outputs[k].Texel.Channels))];
                int pack = packedAt.TryGetValue(members[0].Image, out var had) ? had.Pack : -1;
                if (pack >= 0 && !packs[pack].Members.SequenceEqual(members))
                    throw new BuildProblem(draw.Index, $"{Names(images, members)} share a texture here, but another draw packs {images[members[0].Image].Name} with others.");
                if (pack < 0)
                {
                    foreach (var member in members)
                        if (packedAt.ContainsKey(member.Image))
                            throw new BuildProblem(draw.Index, $"{images[member.Image].Name} is already packed with other images by another draw.");
                    Check(code, images, members, passes, graph, draw.Index);
                    pack = packs.Count;
                    packs.Add((drawn.Outputs[outputs[0]].Texel.Kind, members));
                    foreach (var member in members)
                        packedAt[member.Image] = (pack, member.Channel, member.Channels);
                }
            }
        }

        if (packs.Count == 0)
            return (images, schedule, passes, []);

        // Only draws writing a pack whole, as it was packed, may draw into one.
        foreach (BuildDraw draw in draws)
            foreach (int target in draw.Targets)
                if (packedAt.TryGetValue(target, out var member) && !WritesWhole(draw, passes, packs[member.Pack].Members))
                    throw new BuildProblem(draw.Index,
                        $"{images[target].Name} shares a texture with {Names(images, [.. packs[member.Pack].Members.Where(m => m.Image != target)])}; only a draw writing all of them can draw into it.");

        // The runner's images: each unpacked image as it was, each pack once, at its first member's place.
        var physical = new List<BuildImage>();
        var placeOf = new int[images.Count];
        var result = new List<ImagePack>();
        for (int k = 0; k < images.Count; k++)
        {
            if (!packedAt.TryGetValue(k, out var member))
            {
                placeOf[k] = physical.Count;
                physical.Add(images[k]);
                continue;
            }

            var (kind, members) = packs[member.Pack];
            if (members.Min(m => m.Image) != k)
                continue;
            int used = members.Sum(m => m.Channels);
            var clears = new float[4];
            foreach (var m in members)
                for (int c = 0; c < m.Channels; c++)
                    clears[m.Channel + c] = images[m.Image].Clear;
            int bits = members.Max(m => Texel.Named(images[m.Image].Format)?.Bits ?? 32);
            BuildImage first = images[k];
            string format = Texel.PackedFormat(kind, used, bits);
            int at = physical.Count;
            foreach (var m in members)
                placeOf[m.Image] = at;
            physical.Add(first with
            {
                Name = string.Join("+", members.Select(m => images[m.Image].Name)),
                Format = format,
                Type = Texel.TypeOf(kind, used == 3 ? 4 : used),
                Channels = clears,
            });
            result.Add(new ImagePack(at, [.. members.Select(m => new PackedImage(images[m.Image], m.Channel, m.Channels))]));
        }

        for (int k = 0; k < physical.Count; k++)
            if (physical[k].Source >= 0)
                physical[k] = physical[k] with { Source = placeOf[physical[k].Source] };
        List<BuildPass> renamed = [.. passes.Select(pass => pass.OverImage >= 0 ? pass with { OverImage = placeOf[pass.OverImage] } : pass)];
        return (physical, [.. Renamed(schedule, passes, placeOf)], renamed, result);
    }

    /// <summary>
    /// Whether packing <paramref name="members"/> keeps the graph doing what it says: one size and life, no history on
    /// either side, and every use of each in the code a subscript or <c>GetDimensions</c>, which the packed struct has.
    /// </summary>
    private static void Check(string code, List<BuildImage> images, List<(int Image, int Channel, int Channels)> members, List<BuildPass> passes,
        (int Start, int End) graph, int at)
    {
        BuildImage first = images[members[0].Image];
        foreach (var member in members)
        {
            BuildImage image = images[member.Image];
            if (image.Size != first.Size || image.Life != first.Life)
                throw new BuildProblem(at, $"{Names(images, members)} share a texture here, so they need one size and one life ([Kept], [Cached] or the frame's).");
            if (image.Life == ImageLife.History || images.Any(other => other.Source == member.Image))
                throw new BuildProblem(at, $"{image.Name} shares a texture here, so it can't have a [History] or be one.");
            foreach (Match use in Regex.Matches(code, $@"\b{Regex.Escape(image.Name)}\b"))
            {
                int i = use.Index;
                if ((i >= image.Start && i < image.End) || (i >= graph.Start && i < graph.End)
                    || passes.Any(pass => pass.Library is null && i >= pass.Start && i < pass.End))
                    continue;
                // A member access (s.a) or a declaration of something else by that name (float a;) isn't a use.
                string before = code[..i].TrimEnd();
                if (before.EndsWith('.') || Regex.IsMatch(before, @"\b(?!return\b|else\b|case\b)[A-Za-z_]\w*>?$"))
                    continue;
                string after = code[(i + use.Length)..].TrimStart();
                if (!after.StartsWith('[') && !after.StartsWith(".GetDimensions", StringComparison.Ordinal))
                    throw new BuildProblem(i, $"{image.Name} shares a texture with others (a draw writes more than {DrawOutput.Most} outputs), so read it as {image.Name}[pixel]: it can't be passed on whole.");
            }
        }
    }

    /// <summary>Whether <paramref name="draw"/> writes every member of a pack in one target, at the channels they have.</summary>
    private static bool WritesWhole(BuildDraw draw, List<BuildPass> passes, List<(int Image, int Channel, int Channels)> members)
    {
        if (passes[draw.Pass].Draw is not { } drawn)
            return false;
        DrawLayout layout = DrawLayout.Of(drawn.Outputs);
        for (int t = 0; t < layout.Targets.Count; t++)
        {
            int[] outputs = [.. layout.In(t)];
            if (outputs.Select(k => (draw.Targets[k], layout.Slots[k].Channel, drawn.Outputs[k].Texel.Channels)).SequenceEqual(members))
                return true;
        }

        return false;
    }

    /// <summary>The schedule with every image named as the runner sees it: a packed draw names one texture a target.</summary>
    private static IEnumerable<BuildStep> Renamed(IEnumerable<BuildStep> steps, List<BuildPass> passes, int[] placeOf)
    {
        foreach (BuildStep step in steps)
        {
            yield return step switch
            {
                BuildDraw draw when draw.Exits => draw,
                BuildDraw draw => draw with { Targets = Targets(draw, passes, placeOf) },
                BuildPresent present => present with { Colour = placeOf[present.Colour], Depth = present.Depth < 0 ? -1 : placeOf[present.Depth] },
                BuildBranch branch => branch with { Yes = [.. Renamed(branch.Yes, passes, placeOf)], No = [.. Renamed(branch.No, passes, placeOf)] },
                BuildLoop loop => loop with { Body = [.. Renamed(loop.Body, passes, placeOf)] },
                _ => step,
            };
        }
    }

    /// <summary>A draw's targets: the image (or pack) each of its layout's targets lands in.</summary>
    private static int[] Targets(BuildDraw draw, List<BuildPass> passes, int[] placeOf)
    {
        DrawLayout layout = DrawLayout.Of(passes[draw.Pass].Draw!.Value.Outputs);
        return [.. Enumerable.Range(0, layout.Targets.Count).Select(t => placeOf[draw.Targets[layout.In(t).First()]])];
    }

    /// <summary>Every draw call in the schedule, in loops and branches too.</summary>
    private static IEnumerable<BuildDraw> Draws(IEnumerable<BuildStep> steps)
    {
        foreach (BuildStep step in steps)
        {
            IEnumerable<BuildDraw> found = step switch
            {
                BuildDraw draw => [draw],
                BuildBranch branch => Draws(branch.Yes).Concat(Draws(branch.No)),
                BuildLoop loop => Draws(loop.Body),
                _ => [],
            };
            foreach (BuildDraw draw in found)
                yield return draw;
        }
    }

    private static string Names(List<BuildImage> images, List<(int Image, int Channel, int Channels)> members) =>
        string.Join(", ", members.Select(m => images[m.Image].Name));

    /// <summary>
    /// The declarations Slang sees for a pack: the texture at <paramref name="binding"/> (written at its first member's
    /// place), then for each member a struct reading and writing its channels, and the member as one of those.
    /// </summary>
    public static string Declared(ImagePack pack, BuildImage texture, PackedImage member, bool first, string binding)
    {
        string declared = pack.Declared;
        string type = member.Image.Type;
        string swizzle = DrawLayout.Swizzle(member.Channel, member.Channels);
        string texturePart = first
            ? $"{binding} [[vk::image_format(\"{texture.Format}\")]] RWTexture2D<{texture.Type}> {declared}; "
            : "";
        return texturePart
               + $"struct {declared}_{member.Image.Name} {{ __subscript(uint2 p) -> {type} {{ get {{ return {declared}[p].{swizzle}; }} "
               + $"set {{ {texture.Type} t = {declared}[p]; t.{swizzle} = newValue; {declared}[p] = t; }} }} "
               + $"void GetDimensions(out uint w, out uint h) {{ {declared}.GetDimensions(w, h); }} }}; "
               + $"static {declared}_{member.Image.Name} {member.Image.Name};";
    }
}
