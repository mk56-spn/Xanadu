using System.Collections.Generic;
using MoonWorks.Graphics;
using Friflo.Engine.ECS;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>The images one item's graph works in this frame, each sized by its declaration against the item's box, and its draws' depth.</summary>
/// <param name="Box">The item's pixels on the target: where its images sit, texel (0, 0) at the box's top left.</param>
/// <param name="Extents">By image: the texels that cover the box (the texture may be bigger, its size rounded up).</param>
/// <param name="Stale">Its <c>[Cached]</c> images need filling this frame: just made, or the material changed since.</param>
internal sealed record ItemSet(Texture[] Images, (uint Width, uint Height)[] Extents, Texture? Z, (int X, int Y, int Width, int Height) Box, bool Stale)
{
    /// <summary>Pixels of the box per texel of image <paramref name="k"/>: 1 for a box-sized one.</summary>
    public System.Numerics.Vector2 Stretch(int k) =>
        new((float)Box.Width / Math.Max(1u, Extents[k].Width), (float)Box.Height / Math.Max(1u, Extents[k].Height));

    /// <summary>
    /// The targets the last draw wrote <see cref="Z"/> with this frame (their numbers packed a byte each), or
    /// <see cref="None"/>. Draws into the same images in a row share one depth test; a draw into others (a
    /// shadow map from a lamp) starts it afresh.
    /// </summary>
    public uint DrawnInto { get; set; } = None;

    /// <summary>No draw yet this frame.</summary>
    public const uint None = uint.MaxValue;
}

/// <summary>
/// Frame images for graphs (Docs/render-graph.md, stage B): each item showing a graph takes its own set, the size of its
/// box, cleared to each image's value. A frame's sets go back to the pool when the next frame begins, bucketed by format
/// and size, so items of the same size reuse the same memory; a bucket nothing took for a while is let go. Sizes round
/// up to a step, so a box that moves or grows a little keeps its textures. Images that outlive the frame are the item's
/// own (<see cref="HeldImages"/>), not the pool's. A material's images (fixed size, touched by a <c>[Pass]</c>) are one
/// set for all its items (<see cref="Material"/>), taken the same way but held on the material's entity. A
/// <c>[Shared]</c> image is the frame's: one texture for every graph declaring it (by name and format), the screen's
/// size, taken from the pool and cleared at its first use in the frame. A <c>[View]</c> image is the material's view's
/// texture, drawn before the preludes (<see cref="MaterialViews"/>); clear when the material names no such view.
/// </summary>
internal sealed class ItemImages(GraphicsDevice device, FrameTargets targets, MaterialViews views) : IDisposable
{
    /// <summary>Box sizes round up to a multiple of this, in pixels.</summary>
    private const int SizeStep = 64;

    /// <summary>The key a material's held images go under on its entity; an item's are keyed by where its piece starts.</summary>
    private const uint MaterialKey = uint.MaxValue;

    /// <summary>Frames a free texture waits for a taker before it is let go.</summary>
    private const int Unused = 120;

    private sealed class Pooled(Texture texture)
    {
        public readonly Texture Texture = texture;
        public long Free;
    }

    private readonly Dictionary<(TextureFormat, uint, uint), List<Pooled>> _free = [];
    private readonly List<(TextureFormat, uint, uint, Pooled)> _taken = [];
    private long _frame;

    /// <summary>This frame's <c>[Shared]</c> images, by name and format, with the declaration that took each first.</summary>
    private readonly Dictionary<(string, TextureFormat), (GraphImage Declared, Texture Texture)> _frameShared = [];

    /// <summary>This frame's graphs whose <c>[Shared]</c> image disagreed with the one taken first, and why: each got its own.</summary>
    private readonly Dictionary<CanvasEffect, string> _disagreed = new(ReferenceEqualityComparer.Instance);

    /// <summary>Why one of the effect's <c>[Shared]</c> images couldn't be shared last frame; null when every one was.</summary>
    public string? Disagreement(CanvasEffect effect) => _disagreed.GetValueOrDefault(effect);

    /// <summary>Last frame's sets go back to the pool; textures free too long are let go.</summary>
    public void Begin(long frame)
    {
        _frame = frame;
        foreach ((TextureFormat format, uint width, uint height, Pooled held) in _taken)
        {
            held.Free = frame;
            Bucket((format, width, height)).Add(held);
        }

        _taken.Clear();
        _frameShared.Clear();
        _disagreed.Clear();
        foreach (List<Pooled> bucket in _free.Values)
            for (int k = bucket.Count - 1; k >= 0; k--)
                if (frame - bucket[k].Free > Unused)
                {
                    bucket[k].Texture.Dispose();
                    bucket.RemoveAt(k);
                }
    }

    /// <summary>
    /// A set for an item whose box is <paramref name="box"/>: frame images from the pool, each cleared to its value; the
    /// images the item holds from frame to frame from <paramref name="kept"/> (its range's entity, the piece starting at
    /// <paramref name="start"/>), each history swapped with its source; the material's and the frame's from
    /// <paramref name="material"/>; and a depth when it draws.
    /// </summary>
    public ItemSet Take(CommandBuffer commands, CanvasEffect effect, Entity kept, uint start, (int X, int Y, int Width, int Height) box,
        Texture?[]? material, long stamp)
    {
        uint width = Rounded(box.Width), height = Rounded(box.Height);
        GraphImage[] images = effect.Images;
        bool stale = true;
        Texture?[]? held = effect.HoldsImages && !kept.IsNull
            ? Held(commands, effect, kept, start, width, height, material: false, stamp, out stale)
            : null;
        var taken = new Texture[images.Length];
        var extents = new (uint, uint)[images.Length];
        for (int k = 0; k < taken.Length; k++)
        {
            GraphImage image = images[k];
            extents[k] = image.Screen ? (material![k]!.Width, material[k]!.Height) : image.Size.For(box.Width, box.Height);
            taken[k] = image.Material || image.Screen ? material![k]! : held?[k] ?? FromPool(commands, image, width, height);
        }

        Texture? z = effect.DrawsImages
            ? TakeTexture(targets.DepthFormat, width, height,
                () => Texture.Create2D(device, "graph depth", width, height, targets.DepthFormat, TextureUsageFlags.DepthStencilTarget))
            : null;
        return new ItemSet(taken, extents, z, box, stale);
    }

    /// <summary>
    /// The material's own images this frame, by the graph's places (null for an item's): what its <c>[Pass]</c>es touch,
    /// fixed in size. Frame ones come from the pool; kept ones and histories are held on <paramref name="entity"/>. The
    /// frame's <c>[Shared]</c> images and the material's <c>[View]</c>s are in it too, at their places.
    /// </summary>
    /// <param name="stale">Whether its <c>[Cached]</c> images need filling this frame.</param>
    public Texture?[] Material(CommandBuffer commands, CanvasMaterial owner, CanvasEffect effect, Entity entity, long stamp, out bool stale)
    {
        GraphImage[] images = effect.Images;
        Texture?[] held = Held(commands, effect, entity, MaterialKey, 0, 0, material: true, stamp, out stale);
        var set = new Texture?[images.Length];
        for (int k = 0; k < images.Length; k++)
            if (images[k].View)
                set[k] = views.For(owner, images[k].Name) ?? FromPool(commands, images[k], targets.Width, targets.Height);
            else if (images[k].Shared)
                set[k] = FrameShared(commands, effect, images[k]);
            else if (images[k].Material)
                set[k] = held[k] ?? FromPool(commands, images[k], 0, 0);
        return set;
    }

    /// <summary>
    /// The frame's <c>[Shared]</c> image of this name and format: taken from the pool and cleared by its first graph this
    /// frame, the same texture for every later one. A later declaration of another size or clear gets its own, and its
    /// graph says why (<see cref="Disagreement"/>).
    /// </summary>
    private Texture FrameShared(CommandBuffer commands, CanvasEffect effect, GraphImage image)
    {
        (uint w, uint h) = image.Size.For((int)targets.Width, (int)targets.Height);
        if (!_frameShared.TryGetValue((image.Name, image.Format), out (GraphImage Declared, Texture Texture) shared))
        {
            Texture made = TakeTexture(image.Format, w, h, () => Make(image, w, h));
            Clear(commands, made, image.Clear, cycle: true);
            _frameShared[(image.Name, image.Format)] = (image, made);
            return made;
        }

        if (shared.Declared.Size == image.Size && shared.Declared.Clear == image.Clear)
            return shared.Texture;
        _disagreed[effect] = $"{image.Name} is [Shared], but another graph declares it with another size or clear, so this one gets its own.";
        Texture own = TakeTexture(image.Format, w, h, () => Make(image, w, h));
        Clear(commands, own, image.Clear, cycle: true);
        return own;
    }

    /// <summary>A frame image from the pool, cleared; cycled, since last frame's use may still be in flight.</summary>
    private Texture FromPool(CommandBuffer commands, GraphImage image, uint width, uint height)
    {
        (uint w, uint h) = image.Size.For((int)width, (int)height);
        Texture texture = TakeTexture(image.Format, w, h, () => Make(image, w, h));
        Clear(commands, texture, image.Clear, cycle: true);
        return texture;
    }

    /// <summary>
    /// The held images of one side (the item's, or the <paramref name="material"/>'s) for this frame: each history takes
    /// the texture its source filled last frame, and the source takes the history's, cleared. Made afresh (every one
    /// cleared) for a new compile or a new size. Its cached images are <paramref name="stale"/> when made, or when
    /// <paramref name="stamp"/> (the material's values and buffers) moved since they were filled; they count as filled
    /// from here, since the frame's fill nodes run on them.
    /// </summary>
    private Texture?[] Held(CommandBuffer commands, CanvasEffect effect, Entity kept, uint start, uint width, uint height, bool material,
        long stamp, out bool stale)
    {
        GraphImage[] images = effect.Images;
        if (kept.TryGetRelation(start, out HeldImages had) && had.Effect == effect && had.Width == width && had.Height == height)
        {
            stale = had.Filled != stamp;
            if (stale)
            {
                had.Filled = stamp;
                kept.AddRelation(had);
            }

            for (int k = 0; k < images.Length; k++)
            {
                if (images[k].Life != ImageLife.History || images[k].Material != material)
                    continue;
                int source = images[k].Source;
                (had.Held[k], had.Held[source]) = (had.Held[source], had.Held[k]);
                Clear(commands, had.Held[source]!, images[source].Clear, cycle: true);
            }

            return had.Held;
        }

        if (had.Held is not null)
            had.Dispose();
        var held = new Texture?[images.Length];
        for (int k = 0; k < images.Length; k++)
        {
            GraphImage image = images[k];
            bool hasHistory = Array.Exists(images, other => other.Source == k);
            if (image.Material != material || (image.Life == ImageLife.Frame && !hasHistory))
                continue;
            (uint w, uint h) = image.Size.For((int)width, (int)height);
            held[k] = Make(image, w, h);
            Clear(commands, held[k]!, image.Clear, cycle: false);
        }

        kept.AddRelation(new HeldImages(start, effect, width, height, held, stamp));
        stale = true;
        return held;
    }

    private Texture Make(GraphImage image, uint width, uint height)
    {
        TextureUsageFlags usage = TextureUsageFlags.ComputeStorageRead | TextureUsageFlags.ComputeStorageWrite | TextureUsageFlags.ColorTarget
                                   | TextureUsageFlags.GraphicsStorageRead;
        if (image.Format is not (TextureFormat.R32Uint or TextureFormat.R32G32Uint or TextureFormat.R32G32B32A32Uint
            or TextureFormat.R32Int or TextureFormat.R32G32Int or TextureFormat.R32G32B32A32Int))
            usage |= TextureUsageFlags.Sampler;
        return Texture.Create2D(device, $"graph {image.Name}", width, height, image.Format, usage);
    }

    /// <summary>Every texel set to <paramref name="value"/>. FColor, not Color: Color is bytes, and would clamp a far depth like 1e30 to 1.</summary>
    private static void Clear(CommandBuffer commands, Texture texture, Vector4 value, bool cycle) =>
        commands.EndRenderPass(commands.BeginRenderPass(new ColorTargetInfo(texture, LoadOp.Clear)
        {
            ClearColor = new FColor { R = value.X, G = value.Y, B = value.Z, A = value.W },
            Cycle = cycle,
        }));

    private static uint Rounded(int size) => (uint)((size + SizeStep - 1) / SizeStep * SizeStep);

    private Texture TakeTexture(TextureFormat format, uint width, uint height, Func<Texture> make)
    {
        List<Pooled> bucket = Bucket((format, width, height));
        Pooled held;
        if (bucket.Count > 0)
        {
            held = bucket[^1];
            bucket.RemoveAt(bucket.Count - 1);
        }
        else
            held = new Pooled(make());

        _taken.Add((format, width, height, held));
        return held.Texture;
    }

    private List<Pooled> Bucket((TextureFormat, uint, uint) key)
    {
        if (!_free.TryGetValue(key, out List<Pooled>? bucket))
            _free[key] = bucket = [];
        return bucket;
    }

    public void Dispose()
    {
        Begin(_frame);
        foreach (List<Pooled> bucket in _free.Values)
            foreach (Pooled held in bucket)
                held.Texture.Dispose();
        _free.Clear();
    }
}
