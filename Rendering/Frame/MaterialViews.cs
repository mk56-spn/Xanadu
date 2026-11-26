using System.Collections.Generic;
using MoonWorks.Graphics;
using Xanadu.Rendering.Gpu;
using Xanadu.Ui.Systems.Canvas;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Rendering.Frame;

/// <summary>
/// The frame's <c>[View]</c> images (Docs/render-graph.md, stage F): for every material the frame shows that names a view
/// (<see cref="CanvasMaterial.Views"/>), a job drawing that subtree into a screen-sized rgba8 texture, the one its graph
/// binds under the view's name. A view's own items may show materials with views of their own: those are collected too
/// and drawn first, each (material, name) once a frame, so a cycle ends where it meets a view already taken. A texture no
/// frame asked for in a while is let go.
/// </summary>
internal sealed class MaterialViews(GraphicsDevice device, FrameTargets targets, CanvasTree tree) : IDisposable
{
    /// <summary>Frames a view's texture waits unasked before it is let go.</summary>
    private const int Unused = 120;

    private sealed class Held(Texture texture, int number)
    {
        public readonly Texture Texture = texture;

        /// <summary>The view's own number while it lasts (<see cref="Job.View"/>), from 1.</summary>
        public readonly int Number = number;

        public long Used;
    }

    private readonly Dictionary<CanvasMaterial, Dictionary<string, Held>> _held = new(ReferenceEqualityComparer.Instance);
    private readonly List<Job> _views = [];
    private readonly HashSet<(CanvasMaterial, string)> _taken = [];
    private readonly List<CanvasMaterial> _gone = [];
    private long _frame;
    private int _numbered;

    /// <summary>
    /// Collects the views the frame's jobs ask for, inner ones first, and puts them before every other job; returns how
    /// many there are.
    /// </summary>
    public int Collect(CanvasFrame frame, CanvasCollect collect, HostFramePhases? phases)
    {
        _frame = frame.Number + 1;
        _views.Clear();
        _taken.Clear();
        foreach (Job job in frame.Jobs)
            Visit(job, collect, phases);
        frame.Jobs.InsertRange(0, _views);
        Forget();
        return _views.Count;
    }

    /// <summary>This frame's texture for the material's view <paramref name="name"/>; null when it names none or its root is gone.</summary>
    public Texture? For(CanvasMaterial material, string name) =>
        _held.TryGetValue(material, out Dictionary<string, Held>? views) && views.TryGetValue(name, out Held? held) && held.Used == _frame
            ? held.Texture
            : null;

    /// <summary>The views <paramref name="job"/>'s materials name, each collected after the views its own items ask for.</summary>
    private void Visit(Job job, CanvasCollect collect, HostFramePhases? phases)
    {
        foreach (Entry entry in job.Entries)
        {
            if (entry.Item?.Material is not { Views.Count: > 0 } material)
                continue;
            foreach ((string name, MaterialView view) in material.Views)
            {
                if (!_taken.Add((material, name)) || tree.Item(view.Root) is not { } root)
                    continue;
                Held target = Target(material, name);
                Job drawn = collect.Collect(root, target.Texture, new MwColor(0, 0, 0, 0), phases,
                    view.Transform * Above(root), viewer: material, view: target.Number);
                Visit(drawn, collect, phases);
                _views.Add(drawn);
            }
        }
    }

    /// <summary>Where the tree places <paramref name="item"/>'s parent: the transforms above it, outermost first.</summary>
    private static Transform2D Above(CanvasItem item)
    {
        Transform2D placed = Transform2D.Identity;
        for (CanvasItem? parent = item.Parent; parent is not null; parent = parent.Parent)
            placed = parent.Transform * placed;
        return placed;
    }

    private Held Target(CanvasMaterial material, string name)
    {
        if (!_held.TryGetValue(material, out Dictionary<string, Held>? views))
            _held[material] = views = [];
        if (!views.TryGetValue(name, out Held? held))
            views[name] = held = new Held(Texture.Create2D(device, $"view {name}", targets.Width, targets.Height, TextureFormat.R8G8B8A8Unorm,
                TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler | TextureUsageFlags.ComputeStorageRead
                | TextureUsageFlags.ComputeStorageWrite | TextureUsageFlags.GraphicsStorageRead), ++_numbered);
        held.Used = _frame;
        return held;
    }

    private void Forget()
    {
        _gone.Clear();
        foreach ((CanvasMaterial material, Dictionary<string, Held> views) in _held)
        {
            foreach ((string name, Held held) in views)
                if (_frame - held.Used > Unused)
                {
                    held.Texture.Dispose();
                    views.Remove(name);
                }

            if (views.Count == 0)
                _gone.Add(material);
        }

        foreach (CanvasMaterial material in _gone)
            _held.Remove(material);
    }

    public void Dispose()
    {
        foreach (Dictionary<string, Held> views in _held.Values)
            foreach (Held held in views.Values)
                held.Texture.Dispose();
        _held.Clear();
    }
}
