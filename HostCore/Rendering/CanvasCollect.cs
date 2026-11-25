using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Collecting: walks a root's items in order into a job's flat draw list, placing each (its
/// transform, tint, layer and clip as inherited), handing mounts a collector to place theirs,
/// and sorting the list by layer.
/// </summary>
internal sealed class CanvasCollect(CanvasTree tree, ResidentTrees residents, Rect fullClip)
{
    private readonly CanvasTree _tree = tree;
    private readonly ResidentTrees _residents = residents;
    private readonly Rect _fullClip = fullClip;

    private int _sequence;

    // The walk's timing is exclusive: one clock, each stretch charged to the part running then,
    // so nested parts (a mount placing items whose children hold mounts) never count twice.
    private HostFramePhases? _walkPhases;
    private HostFramePhases.Phase _walkPart;
    private long _walkStamp;

    /// <summary>One root's flat, sorted draw list; the walk and the sort are timed apart, under "collect items".</summary>
    public Job Collect(CanvasItem root, Texture target, MwColor clear, HostFramePhases? phases)
    {
        long lap = HostFramePhases.Now;
        var job = new Job(root, target, clear);
        _sequence = 0;
        _walkPhases = phases;
        _walkStamp = lap;
        _walkPart = HostFramePhases.Phase.CollectCanvas;
        Visit(job, root, Transform2D.Identity, Colors.White, 0, _fullClip);
        Switch(HostFramePhases.Phase.CollectCanvas);
        _walkPhases = null;
        phases?.Lap(HostFramePhases.Phase.CollectWalk, ref lap);
        SortByZ(job.Entries);
        phases?.Lap(HostFramePhases.Phase.CollectSort, ref lap);
        return job;
    }

    private int[] _zStarts = [];
    private Entry[] _zSorted = [];

    /// <summary>
    /// Puts entries in draw order, z then sequence, by counting sort: z is a small
    /// integer and entries are added in sequence order, so counting each z, turning
    /// the counts into start positions and copying entries over in the order they
    /// came keeps ties in sequence for free. Two passes, no comparisons.
    /// </summary>
    private void SortByZ(List<Entry> entries)
    {
        Span<Entry> span = CollectionsMarshal.AsSpan(entries);
        if (span.Length < 2)
            return;

        int min = int.MaxValue, max = int.MinValue;
        foreach (ref readonly Entry entry in span)
        {
            min = Math.Min(min, entry.Z);
            max = Math.Max(max, entry.Z);
        }

        if (min == max)
            return;

        int range = max - min + 1;
        if (_zStarts.Length < range + 1)
            _zStarts = new int[range + 1];
        else
            Array.Clear(_zStarts, 0, range + 1);

        foreach (ref readonly Entry entry in span)
            _zStarts[entry.Z - min + 1]++;
        for (int i = 1; i <= range; i++)
            _zStarts[i] += _zStarts[i - 1];

        if (_zSorted.Length < span.Length)
            _zSorted = new Entry[Math.Max(span.Length, _zSorted.Length * 2)];
        foreach (ref readonly Entry entry in span)
            _zSorted[_zStarts[entry.Z - min]++] = entry;
        _zSorted.AsSpan(0, span.Length).CopyTo(span);
    }

    /// <summary>Charges the walk's time so far to the part that was running and starts <paramref name="next"/>; returns the old part.</summary>
    private HostFramePhases.Phase Switch(HostFramePhases.Phase next)
    {
        HostFramePhases.Phase was = _walkPart;
        _walkPhases?.Lap(was, ref _walkStamp);
        _walkPart = next;
        return was;
    }

    private void Visit(Job job, CanvasItem item, Transform2D parent, Color parentModulate, int parentZ, Rect clip)
    {
        if (!item.Visible)
            return;

        Place(job, item, parent * item.Transform, parentModulate * item.Modulate, Math.Clamp(parentZ + item.Z, -4096, 4096), clip);
    }

    /// <summary>An item already placed (by the tree, or by a mount), then its children and mounts.</summary>
    private void Place(Job job, CanvasItem item, Transform2D transform, Color modulate, int z, Rect clip)
    {
        if (item.Clip && item.UseCustomRect)
            clip = Intersect(clip, Bounds(transform, item.CustomRect));

        if (modulate.A <= 0f || clip.W <= 0 || clip.H <= 0)
            return;

        if (item.Primitives.Count > 0 || item.Particles is { Live: true })
            job.Entries.Add(new Entry(z, _sequence++, item, transform, modulate * item.SelfModulate, clip));

        item.SortChildren();
        foreach (CanvasItem child in item.Children)
            Visit(job, child, transform, modulate, z, clip);

        if (item.Mounts is { } mounts)
        {
            foreach (ICanvasMount mount in mounts)
            {
                HostFramePhases.Phase was = Switch(HostFramePhases.Phase.CollectOtherMounts);
                mount.Collect(new MountCollector(this, job, transform, modulate, z, clip));
                Switch(was);
            }
        }
    }

    /// <summary>Places a mount's items relative to the item it hangs under.</summary>
    private sealed class MountCollector(CanvasCollect renderer, Job job, Transform2D transform, Color modulate, int z, Rect clip)
        : ICanvasCollector
    {
        public void Item(Handle handle, in Transform2D local, Color tint, int layer, Rect2? cut)
        {
            if (renderer._tree.Item(handle) is not { Visible: true } item)
                return;

            HostFramePhases.Phase was = renderer.Switch(HostFramePhases.Phase.CollectCanvas);
            Rect within = cut is { } area ? Intersect(clip, Bounds(transform, area)) : clip;
            renderer.Place(job, item, transform * local * item.Transform, modulate * tint * item.Modulate,
                Math.Clamp(z + layer, -4096, 4096), within);
            renderer.Switch(was);
        }

        public void Begin(MountStep step) => renderer.Switch(step switch
        {
            MountStep.Restructure => HostFramePhases.Phase.CollectUiRestructure,
            _ => HostFramePhases.Phase.CollectUiScan,
        });

        public IResidentTree? Resident(ICanvasMount owner) => renderer._residents.For(owner);

        public void Draw(IResidentTree tree) => renderer.DrawResident(job, tree, transform, modulate, z, clip);
    }

    /// <summary>The tree's pieces into the job: a segment as one entry, a foreign item placed here as the walk placed it.</summary>
    private void DrawResident(Job job, IResidentTree drawn, Transform2D transform, Color modulate, int z, Rect clip)
    {
        if (drawn is not HostResidentTree tree)
            return;

        HostFramePhases.Phase was = Switch(HostFramePhases.Phase.CollectCanvas);
        if (tree.PlanDirty)
            tree.Plan();

        _residents.Drawn(tree, transform, modulate, clip);
        foreach (ResidentPiece piece in tree.Pieces)
        {
            int pz = Math.Clamp(z + piece.Z, -4096, 4096);
            if (!piece.Foreign)
            {
                job.Entries.Add(new Entry(pz, _sequence++, null!, default, default, default, piece));
                continue;
            }

            if (tree.ItemAt(piece.Slot) is not { Visible: true } item ||
                !tree.Resolve(piece.Slot, out Transform2D world, out float alpha, out Rect2? cut))
                continue;

            Rect within = cut is { } area ? Intersect(clip, Bounds(transform, area)) : clip;
            Place(job, item, transform * world * item.Transform, modulate * new Color(1f, 1f, 1f, alpha) * item.Modulate, pz, within);
        }

        Switch(was);
    }


    private static Rect Bounds(Transform2D t, Rect2 rect)
    {
        Vector2 a = t * rect.Position;
        Vector2 b = t * new Vector2(rect.End.X, rect.Position.Y);
        Vector2 c = t * rect.End;
        Vector2 d = t * new Vector2(rect.Position.X, rect.End.Y);
        float minX = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        float minY = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        float maxX = MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X));
        float maxY = MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y));
        int x = (int)MathF.Floor(minX), y = (int)MathF.Floor(minY);
        return new Rect { X = x, Y = y, W = (int)MathF.Ceiling(maxX) - x, H = (int)MathF.Ceiling(maxY) - y };
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        int x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.W, b.X + b.W), bottom = Math.Min(a.Y + a.H, b.Y + b.H);
        return new Rect { X = x, Y = y, W = Math.Max(0, right - x), H = Math.Max(0, bottom - y) };
    }
}
