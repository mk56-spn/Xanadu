using System.Collections.Generic;
using Xanadu.Core.Rendering;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Rendering;

/// <summary>One canvas item: its place in the tree and its primitives, in its own space.</summary>
internal sealed class CanvasItem(ulong id)
{
    public readonly ulong Id = id;
    public CanvasItem? Parent;
    public readonly List<CanvasItem> Children = [];
    public bool ChildrenSorted = true;
    public long Order;
    public int Z;
    public bool Visible = true;
    public Transform2D Transform = Transform2D.Identity;
    public Color Modulate = Colors.White;
    public Color SelfModulate = Colors.White;
    public bool UseCustomRect;
    public Rect2 CustomRect;
    public bool Clip;

    /// <summary>What the item's own draws shade and blend with; null is the plain primitive pipeline.</summary>
    public CanvasMaterial? Material;

    /// <summary>Shaded runs among the item's draws: each primitive's <see cref="Primitive.Run"/> points into this.</summary>
    public readonly List<(CanvasMaterial? Material, IReadOnlyDictionary<string, Vector4>? Values)> Runs = [];

    /// <summary>The run draws added now go into; 0 is plain.</summary>
    public int CurrentRun;

    /// <summary>Set by <c>AddSetTransform</c>: applied to the commands that follow.</summary>
    public Transform2D CommandTransform = Transform2D.Identity;

    public readonly List<Primitive> Primitives = [];

    /// <summary>Draws of many copies of a mesh (<see cref="CanvasTree.AddCopies"/>), each at its place among the primitives.</summary>
    public List<CopyDraw>? Copies;

    /// <summary>
    /// Mesh points added so far: the next command's points are numbered from here, so a
    /// triangle's corner indices are its points in the item, shared by every triangle that uses it.
    /// </summary>
    public int Points;

    /// <summary>GPU particles drawn after the item's own draws; made by the first burst into it.</summary>
    public ParticleField? Particles;

    /// <summary>
    /// Its own draws are the look of each of its particles: drawn once per live
    /// particle of <see cref="Particles"/>, turned, sized, tinted and placed by it,
    /// and not on their own.
    /// </summary>
    public bool ParticleMesh;

    /// <summary>Trees that list their own items here each frame, after this item's children.</summary>
    public List<ICanvasMount>? Mounts;

    /// <summary>Moves on whenever the primitives change, so the renderer knows to send them again.</summary>
    public int Version;

    /// <summary>The resident tree this item draws in, and its slot there; null and -1 when it draws through the walk.</summary>
    public HostResidentTree? ResidentOwner;
    public int ResidentSlot = -1;

    /// <summary>The tree's list of touched items, and whether this one is on it.</summary>
    public List<CanvasItem>? TouchList;
    public bool IsTouched;

    /// <summary>Tells the resident tree this item is in (if any) that its recording or its own place, look or kind changed.</summary>
    public void Touch()
    {
        if (ResidentOwner is null || IsTouched)
            return;

        IsTouched = true;
        TouchList?.Add(this);
    }

    /// <summary>Where the renderer keeps this item's primitives on the GPU, and for which version and pool.</summary>
    public int PoolStart = -1, PoolCapacity, PoolVersion = -1, PoolEpoch = -1;

    /// <summary>A hash of the primitives last sent, so a picture cleared and drawn again the same is not sent again.</summary>
    public int PoolHash;

    /// <summary>Whether <see cref="PoolHash"/> is the hash of what was sent (false when the send skipped hashing).</summary>
    public bool PoolHashKnown;

    /// <summary>Redraws in a row that came out different, and sends that skipped the hash since.</summary>
    public int HashMisses, HashSkips;

    /// <summary>The textures the primitives read, in the order of their local slots on the GPU.</summary>
    public readonly List<ulong> LocalTextures = [];

    public void ClearCommands()
    {
        Version++;
        Touch();
        Primitives.Clear();
        Copies?.Clear();
        Runs.Clear();
        CurrentRun = 0;
        Points = 0;
        CommandTransform = Transform2D.Identity;
    }

    public void SortChildren()
    {
        if (ChildrenSorted)
            return;

        Children.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        ChildrenSorted = true;
    }

    public void Add(in Primitive primitive)
    {
        if (primitive.IsEmpty)
            return;

        Version++;
        Touch();
        Primitive placed = CommandTransform == Transform2D.Identity ? primitive : primitive.Transformed(CommandTransform);
        placed.Run = CurrentRun;
        Primitives.Add(placed);
    }

    /// <summary>A connected line with mitred joints, so a translucent stroke doesn't double up at its corners.</summary>
    public void Strip(Vector2[] points, Color[] colors, float width)
    {
        float half = MathF.Max(width, 1f) / 2f;
        int count = points.Length;
        Span<Vector2> offsets = count <= 256 ? stackalloc Vector2[count] : new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 before = i > 0 ? Normal(points[i - 1], points[i]) : Vector2.Zero;
            Vector2 after = i + 1 < count ? Normal(points[i], points[i + 1]) : Vector2.Zero;
            if (before == Vector2.Zero)
                before = after;
            if (after == Vector2.Zero)
                after = before;

            Vector2 miter = before + after;
            float miterLength = miter.Length();
            if (miterLength < 1e-4f)
            {
                offsets[i] = before * half;
                continue;
            }

            miter /= miterLength;
            float cos = miter.Dot(before);
            offsets[i] = miter * (half / MathF.Max(cos, 0.25f));
        }

        for (int i = 0; i + 1 < count; i++)
        {
            Color a = colors.Length == 1 ? colors[0] : colors[Math.Min(i, colors.Length - 1)];
            Color b = colors.Length == 1 ? colors[0] : colors[Math.Min(i + 1, colors.Length - 1)];
            Vector2 p = points[i], q = points[i + 1];
            int first = Points;
            Add(Primitive.Triangle(p + offsets[i], q + offsets[i + 1], q - offsets[i + 1], a, b, b,
                default, default, default, 0, first, first + 1, first + 2));
            Add(Primitive.Triangle(p + offsets[i], q - offsets[i + 1], p - offsets[i], a, b, a,
                default, default, default, 0, first, first + 2, first + 3));
            Points = first + 4;
        }
    }

    private static Vector2 Normal(Vector2 from, Vector2 to)
    {
        Vector2 along = to - from;
        float length = along.Length();
        return length <= 0f ? Vector2.Zero : new Vector2(-along.Y, along.X) / length;
    }
}

/// <summary>
/// One draw of many copies of a mesh in an item: before primitive <paramref name="At"/>, in run <paramref name="Run"/>
/// (its material), with the copies and their values as they were added (<see cref="CanvasCopy"/>).
/// </summary>
internal sealed record CopyDraw(int At, int Run, CanvasMesh Mesh, CanvasCopy[] Copies, Vector4[] Values);
