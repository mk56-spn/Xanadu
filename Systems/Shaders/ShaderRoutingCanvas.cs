using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Shaders;

/// <summary>Makes and styles the canvas items shaded draws go to.</summary>
public interface IShaderCanvasItems
{
    Rid CreateItem(Rid parent);

    /// <summary>
    /// Gives an item the material of a shader slot, or of a common shader by
    /// path when one is given (it takes priority over the slot), or none when
    /// both are empty, with the blend mode asked for.
    /// </summary>
    void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend);

    /// <summary>Sets one instance uniform on a run item.</summary>
    void SetInstanceUniform(Rid item, string param, Vector4 value, int size) { }

    /// <summary>Makes a run item copy the whole screen to the back buffer before it draws.</summary>
    void CopyScreen(Rid item) { }

    /// <summary>Makes an item copy the given render-target rect into the back buffer before it draws.</summary>
    void CopyRectangle(Rid item, Rect2 pixelRect) { }

    /// <summary>Stops an item copying the back buffer at all.</summary>
    void DisableCopy(Rid item) { }

    /// <summary>Puts an item under another canvas item.</summary>
    void Reparent(Rid item, Rid parent) { }

    /// <summary>Makes an item render its children as one grouped image (a CanvasGroup).</summary>
    void SetCanvasGroup(Rid item) { }

    /// <summary>Multiplies what an item draws by a colour.</summary>
    void SetModulate(Rid item, Color color) { }

    /// <summary>Where an item stacks among its siblings: higher draws in front.</summary>
    void SetDrawIndex(Rid item, int index) { }
}

/// <summary>
/// Wraps a canvas so some of what is drawn into one canvas item can use a
/// shader. A canvas item has a single material, so draws are split into runs:
/// each time the selected slot changes, drawing moves to a fresh child item
/// with that slot's material. Children draw in the order they were made, so
/// the objects still stack exactly as they were drawn.
/// </summary>
/// <remarks>
/// Once any shaded run exists, later unshaded draws also move to child items,
/// since the parent item itself always draws underneath its children.
/// <para>
/// Draw layers (<see cref="BeginLayer"/>) each get a container item under the
/// root, kept from frame to frame, and their runs are children of that
/// container. The root's children (its runs and the containers) are stacked
/// by draw index in the order this frame used them, so a layer made late still
/// draws where it was begun (docs/render-architecture.md §5.1).
/// </para>
/// </remarks>
public sealed class ShaderRoutingCanvas(IRectCanvas inner, Rid root, IShaderCanvasItems items, Func<Rect2, Rect2>? toPixels = null) : IRectCanvas
{
    /// <summary>Turns a canvas rect into render-target pixels; identity when no viewport is known.</summary>
    private readonly Func<Rect2, Rect2> _toPixels = toPixels ?? (static rect => rect);
    private readonly Surface _root = new(root, root, external: false);
    private readonly Dictionary<int, Surface> _layers = new();
    private Surface _current = null!;
    private int _slot;
    private string _commonPath = "";
    private BlendMode _blend = BlendMode.Mix;
    private int _source = -1;
    private readonly List<(string Param, Vector4 Value, int Size)> _values = new();
    private bool _copyScreen;
    private readonly List<(string Param, Vector4 Value, int Size)> _bufferValues = new();
    private Surface? _buffer;
    private Surface? _bufferHost;

    /// <summary>Bumped on every selection or value change, so a run knows to re-apply.</summary>
    private int _stamp;

    /// <summary>The next draw index among the root's children this frame.</summary>
    private int _order;

    /// <summary>How many run items this frame's draws needed, in the root and every layer.</summary>
    public int ItemsUsed
    {
        get
        {
            int used = _root.Used;
            foreach (Surface layer in _layers.Values)
                used += layer.Used;
            return used;
        }
    }

    /// <summary>The container item of a layer begun at least once, for tests and tools.</summary>
    public bool TryGetLayerItem(int layer, out Rid item)
    {
        bool found = _layers.TryGetValue(layer, out Surface? surface);
        item = found ? surface!.Item : default;
        return found;
    }

    public void SelectShader(int slot, BlendMode blend = BlendMode.Mix, int sourceGroup = -1)
    {
        _slot = slot;
        _commonPath = "";
        _blend = blend;
        _source = sourceGroup;
        _values.Clear();
        _stamp++;
    }

    public void SelectCommonShader(string path, BlendMode blend = BlendMode.Mix)
    {
        _commonPath = path ?? "";
        _slot = 0;
        _blend = blend;
        _source = -1;
        _values.Clear();
        _stamp++;
    }

    public void NewRun()
    {
        _current ??= _root;
        _current.HasRun = false;
        _stamp++;
    }

    public void SetShaderValue(string param, Vector4 value, int size)
    {
        if (_buffer is not null)
            _bufferValues.Add((param, value, size));
        else
            _values.Add((param, value, size));
        _stamp++;
    }

    public void CopyScreen()
    {
        _copyScreen = true;
        _stamp++;
    }

    public void CopyScreen(Rect2 bounds)
    {
        _current ??= _root;
        Surface host = _current;
        int index = host.CopiesUsed++;

        if (index == host.Copies.Count)
            host.Copies.Add(new Surface(items.CreateItem(host.Item), host.Item, external: false));

        Surface copy = host.Copies[index];
        copy.Clear(inner, clearItem: true);

        // A self-contained layer draws into its own viewport, where a full copy
        // of that viewport's buffer is both simplest and always in range.
        items.CopyRectangle(copy.Item, host.External ? new Rect2() : _toPixels(bounds));

        // What follows must go into a new run made after this item, or it would
        // draw below the copy and read the stale image instead.
        host.ChildrenSeen = true;
        host.HasRun = false;
    }

    public void BeginBuffer(int shader, BlendMode blend, Color tint, float alpha)
    {
        _current ??= _root;
        Surface host = _current;
        int index = host.GroupUsed++;
        if (index == host.Groups.Count)
        {
            Surface made = new(items.CreateItem(host.Item), host.Item, external: false, group: true);
            items.SetCanvasGroup(made.Item);
            host.Groups.Add(made);
        }

        Surface group = host.Groups[index];
        group.Clear(inner, clearItem: true);
        items.SetMaterial(group.Item, shader, "", blend);
        items.SetModulate(group.Item, new Color(tint.R, tint.G, tint.B, tint.A * alpha));
        _values.Clear();
        _bufferValues.Clear();
        _stamp++;
        _buffer = group;
        _bufferHost = host;
        _current = group;
    }

    public void EndBuffer()
    {
        if (_buffer is null)
            return;

        Surface group = _buffer;
        _buffer = null;
        _current = _bufferHost ?? _root;
        _bufferHost = null;
        foreach ((string param, Vector4 value, int size) in _bufferValues)
            items.SetInstanceUniform(group.Item, param, value, size);
        _bufferValues.Clear();
    }

    public void BeginLayer(int layer, Rid? target = null)
    {
        // A self-contained layer draws straight into its own viewport's canvas
        // item; every other layer gets a container item under the root.
        bool external = target is not null;
        if (!_layers.TryGetValue(layer, out Surface? surface) || surface.External != external)
        {
            surface = external
                ? new Surface(target!.Value, target!.Value, external: true)
                : new Surface(items.CreateItem(root), root, external: false);
            _layers[layer] = surface;
        }

        if (!surface.External)
            items.SetDrawIndex(surface.Item, _order++);
        else
            inner.SetTransform(surface.Item, _rootTransform);

        // What the root draws from here on must stack over this layer, so it
        // can neither go into the root item itself nor into an earlier run.
        _root.ChildrenSeen = true;
        _root.HasRun = false;
        _current = surface;
    }

    public void EndLayers() => _current = _root;

    public void Clear(Rid canvas)
    {
        inner.Clear(canvas);
        if (canvas != root)
            return;

        _root.Clear(inner, clearItem: false);
        foreach (Surface layer in _layers.Values)
            layer.Clear(inner, clearItem: true);

        // A copy item not used this frame must stop copying, or last frame's
        // boundary would fire again at the wrong place in the order.
        ResetCopies(_root);
        foreach (Surface layer in _layers.Values)
            ResetCopies(layer);

        _current = _root;
        _order = 0;
        _slot = 0;
        _commonPath = "";
        _blend = BlendMode.Mix;
        _source = -1;
        _values.Clear();
        _copyScreen = false;
        _buffer = null;
        _bufferHost = null;
        _bufferValues.Clear();
    }

    /// <summary>Stops every copy item under a surface from copying this frame.</summary>
    private void ResetCopies(Surface surface)
    {
        foreach (Surface copy in surface.Copies)
            items.DisableCopy(copy.Item);
        surface.CopiesUsed = 0;
        foreach (Surface group in surface.Groups)
            ResetCopies(group);
    }

    /// <summary>The item a draw aimed at <paramref name="canvas"/> should land in.</summary>
    private Rid Route(Rid canvas)
    {
        if (canvas != root)
            return canvas;

        _current ??= _root;
        Surface surface = _current;
        if (!surface.Group &&
            !surface.ChildrenSeen &&
            _slot == 0 &&
            _commonPath.Length == 0 &&
            _blend == BlendMode.Mix)
        {
            return surface.Item;
        }

        if (surface.HasRun &&
            surface.RunSlot == _slot &&
            surface.RunCommonPath == _commonPath &&
            surface.RunBlend == _blend &&
            surface.RunSource == _source)
        {
            ApplyValues(surface);
            return surface.Run;
        }

        if (surface.Used == surface.Pool.Count)
            surface.Pool.Add(items.CreateItem(surface.Item));

        surface.Run = surface.Pool[surface.Used++];
        surface.RunSlot = _slot;
        surface.RunCommonPath = _commonPath;
        surface.RunBlend = _blend;
        surface.RunSource = _source;
        surface.HasRun = true;
        surface.ChildrenSeen = true;
        items.SetMaterial(surface.Run, _slot, _commonPath, _blend);
        surface.AppliedStamp = -1;
        if (surface == _root)
            items.SetDrawIndex(surface.Run, _order++);
        ApplyValues(surface);
        return surface.Run;
    }

    /// <summary>Writes the instance values set since the run was last picked.</summary>
    private void ApplyValues(Surface surface)
    {
        if (surface.AppliedStamp == _stamp)
            return;

        surface.AppliedStamp = _stamp;
        if (surface.Run != surface.Item)
        {
            foreach ((string param, Vector4 value, int size) in _values)
                items.SetInstanceUniform(surface.Run, param, value, size);
            if (_copyScreen)
                items.CopyScreen(surface.Run);
        }

        _copyScreen = false;
    }

    /// <summary>One item draws are routed into, the root or a layer's container, with its runs.</summary>
    private sealed class Surface(Rid item, Rid parent, bool external, bool group = false)
    {
        public readonly Rid Item = item;
        public Rid Parent = parent;
        public readonly bool External = external;
        public readonly bool Group = group;
        public readonly List<Surface> Groups = new();
        public int GroupUsed;
        public readonly List<Surface> Copies = new();
        public int CopiesUsed;
        public readonly List<Rid> Pool = new();
        public int Used;
        public int RunSlot = -1;
        public string RunCommonPath = "";
        public BlendMode RunBlend = BlendMode.Mix;
        public int RunSource = -1;
        public Rid Run;
        public int AppliedStamp = -1;
        public bool HasRun;

        /// <summary>Once the item has children, plain draws go to children too, or they would sink under them.</summary>
        public bool ChildrenSeen;

        public void Clear(IRectCanvas canvas, bool clearItem)
        {
            if (clearItem)
                canvas.Clear(Item);
            foreach (Rid run in Pool)
                canvas.Clear(run);

            foreach (Surface group in Groups)
                group.Clear(canvas, clearItem: true);
            GroupUsed = 0;
            CopiesUsed = 0;
            Used = 0;
            RunSlot = -1;
            RunCommonPath = "";
            RunBlend = BlendMode.Mix;
            RunSource = -1;
            AppliedStamp = -1;
            HasRun = false;
            ChildrenSeen = false;
        }
    }

    public void SetVisible(Rid canvas, bool visible) => inner.SetVisible(canvas, visible);

    /// <summary>
    /// The root's transform also goes on every self-contained layer's surface,
    /// whose content would otherwise sit unshifted (by the camera) in a surface
    /// only a screen in size, and fall off it.
    /// </summary>
    public void SetTransform(Rid canvas, Transform2D transform)
    {
        inner.SetTransform(canvas, transform);
        if (canvas != root)
            return;

        _rootTransform = transform;
        foreach (Surface layer in _layers.Values)
            if (layer.External)
                inner.SetTransform(layer.Item, transform);
    }

    private Transform2D _rootTransform = Transform2D.Identity;

    /// <summary>A rect on the target in the root's own coordinates, undoing its transform (a pass composite covers the screen).</summary>
    public Rect2 ToLocal(Rect2 screen)
    {
        Transform2D inverse = _rootTransform.AffineInverse();
        Vector2 a = inverse * screen.Position, b = inverse * screen.End;
        return new Rect2(a, Vector2.Zero).Expand(b);
    }

    public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
        inner.DrawRect(Route(canvas), bounds, color);

    public void DrawTexture(Rid canvas, Rect2 bounds, Rid texture) =>
        inner.DrawTexture(Route(canvas), bounds, texture);

    public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
        inner.DrawCircle(Route(canvas), center, radius, color);

    public void DrawGradientRect(Rid canvas, Rect2 bounds, Color start, Color end, bool horizontal) =>
        inner.DrawGradientRect(Route(canvas), bounds, start, end, horizontal);

    public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
        inner.DrawPolygon(Route(canvas), points, color);

    public void DrawTriangles(Rid canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors) =>
        inner.DrawTriangles(Route(canvas), points, colors);

    public void DrawTriangles(Rid canvas, ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs) =>
        inner.DrawTriangles(Route(canvas), points, colors, uvs);

    public void DrawRectOutlines(Rid canvas, ReadOnlySpan<Rect2> rects, Color color) =>
        inner.DrawRectOutlines(Route(canvas), rects, color);

    public void DrawPolyline(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        Color color,
        float width,
        bool antialiased = true) =>
        inner.DrawPolyline(Route(canvas), points, color, width, antialiased);

    public void DrawCircleOutline(
        Rid canvas,
        Vector2 center,
        float radius,
        Color color,
        float width,
        int segments = 40) =>
        inner.DrawCircleOutline(Route(canvas), center, radius, color, width, segments);

    public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
        inner.DrawLine(Route(canvas), from, to, color, width);

    public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawText(Route(canvas), bounds, text, color);

    public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyled(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawTextLeft(Route(canvas), bounds, text, color);

    public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        inner.DrawTextRight(Route(canvas), bounds, text, color);

    public void DrawTextTurned(Rid canvas, Rect2 bounds, float degrees, string text, Color color, int fontSize) =>
        inner.DrawTextTurned(Route(canvas), bounds, degrees, text, color, fontSize);

    public void DrawTextVertical(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextVertical(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextStyledLeft(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyledLeft(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextStyledRight(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
        inner.DrawTextStyledRight(Route(canvas), bounds, text, color, fontSize);

    public void DrawTextWeighted(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold) =>
        inner.DrawTextWeighted(Route(canvas), bounds, text, color, fontSize, align, bold);

    public void DrawTextMono(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align) =>
        inner.DrawTextMono(Route(canvas), bounds, text, color, fontSize, align);

    public void DrawTextGlow(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        TextGlow glow,
        TextOutline? outline = null) =>
        inner.DrawTextGlow(Route(canvas), bounds, text, color, fontSize, align, bold, glow, outline);

    public void DrawShapeGlow(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        bool closed,
        bool filled,
        float strokeWidth,
        ShapeGlow glow) =>
        inner.DrawShapeGlow(Route(canvas), points, closed, filled, strokeWidth, glow);

    public void DrawTextOutline(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        int size) =>
        inner.DrawTextOutline(Route(canvas), bounds, text, color, fontSize, align, bold, size);
}

/// <summary>
/// Shader canvas items on the <see cref="CanvasServer"/>: each run item gets a
/// <see cref="CanvasMaterial"/> for its common shader and blend, and instance
/// values go on the item itself. Level shader slots draw plain until they move
/// to the slot model (docs/moonworks-port.md step 5); so do screen copies until
/// step 6.
/// </summary>
public class ServerShaderCanvasItems : IShaderCanvasItems
{
    // One material per (shader, blend), shared by every run item that uses it.
    private static readonly Dictionary<(string, BlendMode), CanvasMaterial> Materials = new();

    /// <summary>A level slot's material with a blend; set each frame from the level being drawn.</summary>
    public Func<int, BlendMode, CanvasMaterial?> SlotMaterialOf { get; set; } = static (_, _) => null;

    public Rid CreateItem(Rid parent)
    {
        Rid item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        return item;
    }

    public virtual void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend)
    {
        if (commonPath.Length == 0 && slot != 0 && SlotMaterialOf(slot, blend) is { } slotMaterial)
        {
            CanvasServer.Current.SetMaterial(item, slotMaterial);
            return;
        }

        string shader = commonPath;
        if (shader.Length == 0 && blend == BlendMode.Mix)
        {
            CanvasServer.Current.SetMaterial(item, null);
            return;
        }

        if (!Materials.TryGetValue((shader, blend), out CanvasMaterial? material))
        {
            material = new CanvasMaterial(shader, blend);
            Materials[(shader, blend)] = material;
        }

        CanvasServer.Current.SetMaterial(item, material);
    }

    public void SetInstanceUniform(Rid item, string param, Vector4 value, int size) =>
        CanvasServer.Current.SetInstanceValue(item, param, value);

    // The host copies the whole target whenever a screen reader needs a fresh
    // copy (docs/moonworks-port.md step 6), so a region is copied whole.
    public void CopyScreen(Rid item) => CanvasServer.Current.SetCopyScreen(item, true);

    public void CopyRectangle(Rid item, Rect2 pixelRect) => CanvasServer.Current.SetCopyScreen(item, true);

    public void DisableCopy(Rid item) => CanvasServer.Current.SetCopyScreen(item, false);

    // The C# defaults of CanvasItemSetCanvasGroupMode are not the CanvasGroup
    // node's (clear margin 5, fit empty false, fit margin 0): with fit_empty
    // off the cull never makes the group's own draw command, so the group's
    // self modulate is ignored and a faded buffer renders fully opaque. Pass
    // the node's values explicitly.
    public void SetCanvasGroup(Rid item) =>
        CanvasServer.Current.SetCanvasGroup(item, clearMargin: 10f, fitEmpty: true, fitMargin: 10f);

    public void SetModulate(Rid item, Color color) => CanvasServer.Current.SetSelfModulate(item, color);

    public void Reparent(Rid item, Rid parent) => CanvasServer.Current.SetParent(item, parent);

    public void SetDrawIndex(Rid item, int index) => CanvasServer.Current.SetDrawIndex(item, index);
}

/// <summary>Shader canvas items with Godot materials, back-buffer copies and instance uniforms.</summary>
public sealed class GodotShaderCanvasItems : ServerShaderCanvasItems, IShaderCanvasItems
{
    /// <summary>Looks up the material of a slot and blend; set each frame from the level being drawn.</summary>
    public Func<int, BlendMode, Rid> MaterialOf { get; set; } = static (_, _) => default;

    /// <summary>Looks up a common shader's material by resource path and blend.</summary>
    public Func<string, BlendMode, Rid> CommonMaterialOf { get; set; } = static (_, _) => default;

    /// <summary>Looks up the material plain (unshaded) draws use for a blend mode.</summary>
    public Func<BlendMode, Rid> PlainMaterialOf { get; set; } = PlainBlendMaterials.MaterialOf;

    public override void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend)
    {
        Rid material = commonPath.Length > 0
            ? CommonMaterialOf(commonPath, blend)
            : slot != 0 ? MaterialOf(slot, blend)
            : blend != BlendMode.Mix ? PlainMaterialOf(blend)
            : default;
        RenderingServer.CanvasItemSetMaterial(item, material);
    }

    public void CopyScreen(Rid item) =>
        RenderingServer.CanvasItemSetCopyToBackbuffer(item, true, new Rect2());

    public void CopyRectangle(Rid item, Rect2 pixelRect) =>
        RenderingServer.CanvasItemSetCopyToBackbuffer(item, true, pixelRect);

    public void DisableCopy(Rid item) =>
        RenderingServer.CanvasItemSetCopyToBackbuffer(item, false, new Rect2());

    public void SetInstanceUniform(Rid item, string param, Vector4 value, int size)
    {
        Variant variant = size switch
        {
            1 => value.X,
            2 => new Vector2(value.X, value.Y),
            3 => new Vector3(value.X, value.Y, value.Z),
            _ => value,
        };
        RenderingServer.CanvasItemSetInstanceShaderParameter(item, param, variant);
    }
}
