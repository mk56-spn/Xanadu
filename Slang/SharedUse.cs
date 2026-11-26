using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// The <c>[Shared]</c> lists and images a graph writes and reads, each by its key: what the runner orders materials by,
/// writers before readers (Docs/render-graph.md, stage F). Worked out once a compile, over every pass whatever its
/// values call: a graph that may write a shared resource goes before every graph that reads it.
/// </summary>
/// <param name="Writes">The keys of what any of its passes writes.</param>
/// <param name="Reads">The keys of what any of its passes reads.</param>
public sealed record SharedUse(string[] Writes, string[] Reads)
{
    public static readonly SharedUse None = new([], []);

    public bool Any => Writes.Length > 0 || Reads.Length > 0;

    /// <summary>What names a shared image across materials: its name and texel format.</summary>
    public static string Key(BuildImage image) => $"image {image.Name} {image.Format}";

    /// <summary>What names a shared list across materials: its name and element type.</summary>
    public static string Key(BuildBuffer buffer) => $"list {buffer.Name} {buffer.Type}";

    /// <summary>What <paramref name="passes"/> do with the graph's shared lists and images.</summary>
    public static SharedUse Of(StackBuild build, CompiledPass[] passes)
    {
        if (!build.Images.Any(image => image.Shared) && !build.Buffers.Any(buffer => buffer.Shared))
            return None;
        HashSet<string> writes = [], reads = [];

        void Image(int k, GraphAccess access)
        {
            if (!build.Images[k].Shared)
                return;
            if (access.HasFlag(GraphAccess.Write))
                writes.Add(Key(build.Images[k]));
            if (access.HasFlag(GraphAccess.Read))
                reads.Add(Key(build.Images[k]));
        }

        void Buffer(int k, GraphAccess access)
        {
            if (!build.Buffers[k].Shared)
                return;
            if (access.HasFlag(GraphAccess.Write))
                writes.Add(Key(build.Buffers[k]));
            if (access.HasFlag(GraphAccess.Read))
                reads.Add(Key(build.Buffers[k]));
        }

        foreach (CompiledPass pass in passes)
        {
            for (int at = 0; at < pass.Images.Length; at++)
                Image(pass.Images[at], pass.ImageAccess[at]);
            foreach (int image in pass.Sampled)
                Image(image, GraphAccess.Read);
            for (int at = 0; at < pass.Buffers.Length; at++)
                Buffer(pass.Buffers[at], pass.Access[at]);
            if (pass.Drawn is not { } drawn)
                continue;
            foreach (int image in drawn.Reads)
                Image(image, GraphAccess.Read);
            foreach (int buffer in drawn.Vertex.StackBuffers.Concat(drawn.Fragment.StackBuffers))
                Buffer(buffer, GraphAccess.Read);
        }

        return new SharedUse([.. writes.Order()], [.. reads.Order()]);
    }
}
