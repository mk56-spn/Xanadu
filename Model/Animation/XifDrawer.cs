using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xanadu.Core.Items;
using Xanadu.Core.Layering;
using Xanadu.Core.Rendering;
using Xanadu.Model.Levels;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Model.Animation;

/// <summary>
/// A colour laid over everything one draw makes, by whoever draws it: the
/// colours are multiplied by <see cref="Modulate"/> (alpha included), then
/// mixed towards <see cref="Flash"/> by <see cref="FlashAmount"/>, as a hit
/// flashes the body white.
/// </summary>
public readonly record struct XifTint(Color Modulate, Color Flash, float FlashAmount)
{
    public static XifTint Plain => new(Colors.White, Colors.White, 0f);

    public static XifTint Faded(float alpha) => new(new Color(1f, 1f, 1f, alpha), Colors.White, 0f);

    public Color Apply(Color colour)
    {
        Color tinted = colour * Modulate;
        if (FlashAmount <= 0f)
            return tinted;
        float t = Math.Clamp(FlashAmount, 0f, 1f);
        return new Color(
            tinted.R + (Flash.R - tinted.R) * t,
            tinted.G + (Flash.G - tinted.G) * t,
            tinted.B + (Flash.B - tinted.B) * t,
            tinted.A);
    }
}

/// <summary>
/// The one way a Xif is drawn, in play, in the editor and in the animator:
/// every node's geometry becomes triangles, drawn into an
/// <see cref="IRectCanvas"/> each frame, back to front by layer. A plain node
/// is drawn once, or once per copy of its instancer; a rig's parts ride their
/// bones (bent by them when skinned), its chains lie along their simulated
/// points and its cloths' art is laid over their sheets. A node with a common shader is
/// drawn in a run of its own with its params. See Docs/xif-modules.md.
/// </summary>
/// <remarks>
/// What a shader is handed (the seam the shader stack keeps): uv runs 0..1
/// across the geometry's box (y down), and uv.x's whole part is the copy's
/// number, its fraction the place across; <c>xif_unit</c> is target pixels
/// per Xif unit and <c>xif_seed</c> the instancer's seed. The node's params
/// go in by name. Triangulations are kept per geometry until
/// <see cref="Forget"/>, so whoever edits geometry in place calls it.
/// </remarks>
public sealed partial class XifDrawer
{
    /// <summary>One drawer for everything that never edits what it draws (play, the level editor).</summary>
    public static XifDrawer Shared { get; } = new();

    /// <summary>Keeps a copy's place across from reaching 1, so its whole part stays the copy's number.</summary>
    private const float AcrossTop = 0.999f;

    private ConditionalWeakTable<XifGeometry, ArtMesh> _meshes = [];

    private readonly List<Batch> _batches = [];
    private readonly List<Vector2> _points = [];
    private readonly List<Color> _colors = [];
    private readonly List<Vector2> _uvs = [];

    /// <summary>Per point, the curve tag of its triangle (<see cref="ArtMesh.Curves"/>); 0 for plain.</summary>
    private readonly List<byte> _tags = [];
    private readonly List<byte> _batchTags = [];
    private readonly List<XifInstance> _copies = [];

    /// <summary>Drops every triangulation and bent mesh kept, after geometry was edited in place.</summary>
    public void Forget()
    {
        _meshes = [];
        _centred = [];
        _dense = [];
        _cuts = [];
        _bent = [];
        _drawnMeshes.Clear();
    }

    /// <summary>A geometry's triangles, kept while the geometry lives.</summary>
    public ArtMesh MeshOf(XifGeometry geometry) => _meshes.GetValue(geometry, static g => ArtMesh.Of(g));

    /// <summary>
    /// Draws one node on its own (not its children): its geometry placed by
    /// <paramref name="placement"/> (node space to target), once per copy of its
    /// instancer, shaded by its common shader. <paramref name="unit"/> is target
    /// pixels per node unit, for the shader.
    /// </summary>
    public void DrawNode(IRectCanvas canvas, Handle target, Xif node, Transform2D placement, float unit, XifTint tint)
    {
        if (node.Get<XifGeometry>() is not { } geometry)
            return;
        ArtMesh mesh = MeshOf(geometry);
        if (mesh.Triangles.Length == 0)
            return;

        XifInstancer? instancer = node.Get<XifInstancer>();
        _copies.Clear();
        if (instancer is not null)
            XifInstances.Place(instancer, _copies);
        else
            _copies.Add(new XifInstance(0, Vector2.Zero, 1f, 0f));

        Begin();
        int start = _points.Count;
        foreach (XifInstance copy in _copies)
        {
            for (int k = 0; k < mesh.Triangles.Length; k++)
            {
                int index = mesh.Triangles[k];
                Vector2 point = mesh.Points[index];
                Push(placement * copy.Place(point), tint.Apply(mesh.Colors[index]), UvOf(point, mesh.Bounds, copy.Index), mesh.CurveAt(k));
            }
        }
        _batches.Add(BatchOf(node, 0, 0, start, instancer?.Seed ?? 0u, unit, mesh.Bounds));
        Flush(canvas, target);
    }

    /// <summary>
    /// Which parts a rig draw takes: all (the default), all but <see cref="Skip"/>,
    /// or <see cref="Only"/> one, with no chains or cloth.
    /// </summary>
    public readonly record struct PartFilter(int Only = -1, int Skip = -1)
    {
        public PartFilter() : this(-1) { }
    }

    /// <summary>
    /// Draws a rig as posed: every part on its bone (its pose picks the art, the
    /// layer and how see-through, or hides it), every chain along
    /// <paramref name="chains"/>' points and every cloth through
    /// <paramref name="cloths"/>', back to front by layer. <paramref name="placement"/>
    /// maps rig space to the target.
    /// </summary>
    public void DrawRig(
        IRectCanvas canvas,
        Handle target,
        RigPackage package,
        RigPose pose,
        Transform2D[] bones,
        Transform2D[] attachments,
        RigChainSimulation? chains,
        RigClothSimulation? cloths,
        Transform2D placement,
        float unit,
        XifTint tint,
        PartFilter parts = default)
    {
        Begin();
        _drawnMeshes.Clear();
        CollectParts(package, pose, bones, attachments, placement, unit, tint, parts);
        if (parts.Only >= 0)
        {
            // One part alone, as an editor holding it draws it over everything else.
            Flush(canvas, target);
            return;
        }
        if (chains is not null)
            CollectChains(package, chains, placement, unit, tint);
        if (cloths is not null)
            CollectCloths(package, cloths, placement, unit, tint);
        Flush(canvas, target);
    }

    private void CollectParts(RigPackage package, RigPose pose, Transform2D[] bones, Transform2D[] attachments,
        Transform2D placement, float unit, XifTint tint, PartFilter filter)
    {
        Transform2D[]? restBones = null;
        Transform2D[]? restAttachments = null;
        for (int i = 0; i < package.Attachments.Count && i < attachments.Length; i++)
        {
            if (i == filter.Skip || (filter.Only >= 0 && i != filter.Only))
                continue;
            RigAttachment part = package.Attachments[i];
            // A particle node's geometry is its particles' look (RigParticleStreams), not drawn at its spot.
            if (part.Particles is not null)
                continue;
            RigAttachmentState state = i < pose.AttachmentStates.Length ? pose.AttachmentStates[i] : default;
            if (i >= pose.AttachmentStates.Length && part.Hidden)
                continue;
            float alpha = state.Alpha;
            if (alpha <= 0f)
                continue;

            int art = state.Art >= 0 && state.Art < part.ArtCount ? state.Art : 0;
            XifGeometry geometry = part.Art(art);
            ArtMesh mesh = MeshOf(geometry);
            if (mesh.Triangles.Length == 0)
                continue;

            var partTint = tint with { Modulate = tint.Modulate * part.SelfModulate * new Color(1f, 1f, 1f, alpha) };
            int layer = DrawOrder.ClampLayer(state.LayerOf(part));
            int start = _points.Count;
            if (part.Mesh is { } skin)
            {
                if (restBones is null || restAttachments is null)
                {
                    RigPose rest = RigClothLayout.RestPose(package);
                    restBones = RigRig.WorldBones(package, rest);
                    restAttachments = RigRig.WorldAttachments(package, rest, restBones);
                }
                if (Bend(package, pose, part, skin, geometry, mesh, bones, attachments[i], restBones, restAttachments[i], placement) is { } bent)
                {
                    _drawnMeshes[i] = (bent.Drawn, bent.Triangles);
                    foreach (int index in bent.Triangles)
                        Push(bent.Drawn[index], partTint.Apply(bent.Colors[index]), UvOf(bent.Local[index], mesh.Bounds, 0));
                }
            }
            else
            {
                Transform2D at = placement * attachments[i];
                for (int k = 0; k < mesh.Triangles.Length; k++)
                {
                    int index = mesh.Triangles[k];
                    Push(at * mesh.Points[index], partTint.Apply(mesh.Colors[index]), UvOf(mesh.Points[index], mesh.Bounds, 0), mesh.CurveAt(k));
                }
            }
            if (_points.Count > start)
                _batches.Add(BatchOf(part.Node, layer, AnimatorDrawOrder.Attachment(i), start, 0u, unit, mesh.Bounds));
        }
    }

    private void CollectChains(RigPackage package, RigChainSimulation simulation, Transform2D placement, float unit, XifTint tint)
    {
        for (int c = 0; c < package.Chains.Count; c++)
        {
            RigChain chain = package.Chains[c];
            if (chain.Node.Get<XifGeometry>() is not { } geometry)
                continue;
            ArtMesh art = MeshOf(geometry);
            if (art.Triangles.Length == 0)
                continue;

            IReadOnlyList<Vector2> points = simulation.Points(c);
            int segments = Math.Max(0, points.Count - 1);
            if (segments == 0)
                continue;
            int layer = DrawOrder.ClampLayer(RigChainArt.LayerOf(package, chain));
            int start = _points.Count;

            if (chain.Sheet)
            {
                (Vector2[] strip, Vector2[] uvs, int[] triangles) = RigChainArt.Sheet(points, chain.Width);
                if (triangles.Length == 0)
                    continue;
                ArtMesh dense = DenseOf(geometry, Math.Max(1, chain.Segments));
                Vector2 size = dense.Bounds.Size.Max(Vector2.One);
                foreach (int index in dense.Triangles)
                {
                    Vector2 uv = (dense.Points[index] - dense.Bounds.Position) / size;
                    Push(placement * RigChainArt.OnSheet(strip, uvs, uv), tint.Apply(dense.Colors[index]), uv);
                }
                _batches.Add(BatchOf(chain.Node, layer, AnimatorDrawOrder.Chain(c, 0), start, 0u, unit));
                continue;
            }

            ArtMesh centred = CentredOf(geometry);
            for (int s = 0; s < segments; s++)
            {
                int piece = _points.Count;
                Transform2D at = placement * RigChainArt.Segment(points[s], points[s + 1], s, segments, chain.Width, centred.Bounds.Size);
                foreach (int index in centred.Triangles)
                    Push(at * centred.Points[index], tint.Apply(centred.Colors[index]), UvOf(centred.Points[index], centred.Bounds, s));
                _batches.Add(BatchOf(chain.Node, layer, AnimatorDrawOrder.Chain(c, s), piece, 0u, unit));
            }
        }
    }

    private void CollectCloths(RigPackage package, RigClothSimulation simulation, Transform2D placement, float unit, XifTint tint)
    {
        for (int c = 0; c < package.Cloths.Count; c++)
        {
            RigCloth cloth = package.Cloths[c];
            if (cloth.Node.Get<XifGeometry>() is not { } geometry)
                continue;
            IReadOnlyList<Vector2> points = simulation.Points(c);
            if (points.Count != cloth.Count || cloth.Columns < 2 || cloth.Rows < 2)
                continue;

            RigClothArt.Cut cut = CutOf(geometry, cloth.Columns, cloth.Rows);
            if (cut.Triangles.Length == 0)
                continue;
            int start = _points.Count;
            foreach (int index in cut.Triangles)
            {
                Vector2 uv = cut.Uvs[index];
                Push(placement * RigClothArt.On(points, cloth.Columns, cut.Cells[index], cut.InCell[index]),
                    tint.Apply(cut.Colors[index]), new Vector2(Math.Clamp(uv.X, 0f, AcrossTop), uv.Y));
            }
            _batches.Add(BatchOf(cloth.Node, DrawOrder.ClampLayer(cloth.ZOrder), AnimatorDrawOrder.Cloth(c), start, 0u, unit, cut.Bounds));
        }
    }

    private ConditionalWeakTable<XifGeometry, Dictionary<(int, int), RigClothArt.Cut>> _cuts = [];

    /// <summary>A geometry cut along a cloth grid, kept per grid size while the geometry lives.</summary>
    private RigClothArt.Cut CutOf(XifGeometry geometry, int columns, int rows)
    {
        Dictionary<(int, int), RigClothArt.Cut> byGrid = _cuts.GetOrCreateValue(geometry);
        if (!byGrid.TryGetValue((columns, rows), out RigClothArt.Cut? cut))
            byGrid[(columns, rows)] = cut = RigClothArt.Of(MeshOf(geometry), columns, rows);
        return cut;
    }

    // --- emitting -------------------------------------------------------------

    private void Begin()
    {
        _batches.Clear();
        _points.Clear();
        _colors.Clear();
        _uvs.Clear();
        _tags.Clear();
    }

    private void Push(Vector2 point, Color colour, Vector2 uv, byte curve = 0)
    {
        _points.Add(point);
        _colors.Add(colour);
        _uvs.Add(uv);
        _tags.Add(curve);
    }

    /// <summary>The batch's curve tags, one per triangle, or null when it has no curves.</summary>
    private ReadOnlySpan<byte> CurveTags(Batch batch)
    {
        _batchTags.Clear();
        bool any = false;
        for (int i = batch.Start; i + 2 < batch.Start + batch.Count && i < _tags.Count; i += 3)
        {
            _batchTags.Add(_tags[i]);
            any |= _tags[i] != 0;
        }
        return any ? CollectionsMarshal.AsSpan(_batchTags) : default;
    }

    /// <summary>Draws the batches back to front: by layer, then order, then the order they were made in.</summary>
    private void Flush(IRectCanvas canvas, Handle target)
    {
        for (int i = 0; i < _batches.Count; i++)
            _batches[i] = _batches[i] with { Made = i };
        _batches.Sort(static (a, b) =>
            a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) :
            a.Order != b.Order ? a.Order.CompareTo(b.Order) :
            a.Made.CompareTo(b.Made));

        Span<Vector2> points = CollectionsMarshal.AsSpan(_points);
        Span<Color> colors = CollectionsMarshal.AsSpan(_colors);
        Span<Vector2> uvs = CollectionsMarshal.AsSpan(_uvs);
        foreach (Batch batch in _batches)
        {
            if (batch.Count < 3)
                continue;
            bool shaded = batch.Shader.Length > 0;
            if (shaded)
                SelectShader(canvas, batch);
            ReadOnlySpan<byte> curves = CurveTags(batch);
            if (!curves.IsEmpty)
            {
                canvas.DrawCurveTriangles(target,
                    points.Slice(batch.Start, batch.Count),
                    colors.Slice(batch.Start, batch.Count),
                    uvs.Slice(batch.Start, batch.Count),
                    curves);
            }
            else
            {
                canvas.DrawTriangles(target,
                    points.Slice(batch.Start, batch.Count),
                    colors.Slice(batch.Start, batch.Count),
                    uvs.Slice(batch.Start, batch.Count));
            }
            if (shaded)
                canvas.SelectCommonShader("");
        }
        _batches.Clear();
    }

    private void SelectShader(IRectCanvas canvas, in Batch batch)
    {
        canvas.SelectCommonShader(batch.Shader);
        canvas.NewRun();
        if (batch.Params is { } shader)
        {
            foreach (KeyValuePair<string, float[]> param in shader.Params)
            {
                float[] v = param.Value;
                if (v.Length == 0)
                    continue;
                var value = new Vector4(v[0], v.Length > 1 ? v[1] : 0f, v.Length > 2 ? v[2] : 0f, v.Length > 3 ? v[3] : 0f);
                canvas.SetShaderValue(param.Key, value, Math.Min(v.Length, 4));
            }
        }
        // Fitted, the [Fit] settings take x and y from the shape, keeping the rest.
        if (batch.Params is { Fit: true } fitted && batch.HalfSize != Vector2.Zero)
        {
            foreach ((string name, Vector4 fallback, int channels) in FittedOf(batch.Shader))
            {
                // z and w are the item's, or else the shader's own.
                float[] kept = fitted.Params.TryGetValue(name, out float[]? set) ? set : [];
                var value = new Vector4(batch.HalfSize.X, batch.HalfSize.Y,
                    kept.Length > 2 ? kept[2] : fallback.Z, kept.Length > 3 ? kept[3] : fallback.W);
                canvas.SetShaderValue(name, value, channels);
            }
        }
        canvas.SetShaderValue("xif_unit", new Vector4(batch.Unit, 0f, 0f, 0f), 1);
        canvas.SetShaderValue("xif_seed", new Vector4(batch.Seed, 0f, 0f, 0f), 1);
    }

    /// <summary>The vertices pushed since <paramref name="start"/> as one batch, drawn with the node's shader.</summary>
    private Batch BatchOf(Xif node, int layer, int order, int start, uint seed, float unit, Rect2 bounds = default)
    {
        string shader = ShaderOf(node);
        return new Batch(layer, order, start, _points.Count - start, shader, seed, unit)
        {
            Params = shader.Length > 0 ? node.Get<XifShader>() : null,
            HalfSize = bounds.Size / 2,
        };
    }

    private readonly Dictionary<string, (string Name, Vector4 Default, int Channels)[]> _fitted = [];

    /// <summary>The settings a shader tags <c>[Fit]</c>, with their defaults, as the host's compiler reports them; kept per shader.</summary>
    private (string Name, Vector4 Default, int Channels)[] FittedOf(string shader)
    {
        if (_fitted.TryGetValue(shader, out (string, Vector4, int)[]? known))
            return known;
        var found = new List<(string, Vector4, int)>();
        if (Xanadu.Ui.Systems.HostPlatform.DescribeMaterial?.Invoke(new CanvasMaterial(shader)) is { } settings)
        {
            foreach (MaterialParam param in settings)
            {
                if (param.Fit)
                    found.Add((param.Name, param.Default, param.Channels));
            }
        }
        return _fitted[shader] = [.. found];
    }

    /// <summary>
    /// The common shader a node draws with, when it names one the game knows, with its
    /// known layers stacked over it (<see cref="ShaderStackKey"/>); empty for plain.
    /// </summary>
    private static string ShaderOf(Xif node)
    {
        if (node.Get<XifShader>() is not { } module)
            return "";

        XifShaderRef shader = module.Shader;
        bool based = shader.Scope == XifShaderScope.Common && CommonShaders.IsKnown(shader.Path);
        if (module.Layers.Count == 0)
            return based ? shader.Path : "";

        var stack = new List<string>(module.Layers.Count + 1);
        if (based)
            stack.Add(shader.Path);
        foreach (string layer in module.Layers)
        {
            if (CommonShaders.IsKnown(layer))
                stack.Add(layer);
        }
        return ShaderStackKey.Of(stack);
    }

    private static Vector2 UvOf(Vector2 point, Rect2 bounds, int copy)
    {
        Vector2 size = new(MathF.Max(bounds.Size.X, 1e-3f), MathF.Max(bounds.Size.Y, 1e-3f));
        Vector2 uv = (point - bounds.Position) / size;
        return new Vector2(copy + Math.Clamp(uv.X, 0f, AcrossTop), uv.Y);
    }

    private ConditionalWeakTable<XifGeometry, ArtMesh> _centred = [];
    private ConditionalWeakTable<XifGeometry, Dictionary<int, ArtMesh>> _dense = [];

    private ArtMesh CentredOf(XifGeometry geometry) => _centred.GetValue(geometry, g => MeshOf(g).Centred());

    private ArtMesh DenseOf(XifGeometry geometry, int segments)
    {
        Dictionary<int, ArtMesh> bySegments = _dense.GetOrCreateValue(geometry);
        if (!bySegments.TryGetValue(segments, out ArtMesh? mesh))
            bySegments[segments] = mesh = ArtMesh.Dense(geometry, RigChainArt.SheetSteps * segments * 2);
        return mesh;
    }

    /// <summary>
    /// One run of triangles: where it sits in the draw (layer, order, and the
    /// order it was made in), its vertices, and the shader and values it takes.
    /// </summary>
    private readonly record struct Batch(int Layer, int Order, int Start, int Count, string Shader, uint Seed, float Unit)
    {
        public int Made { get; init; }

        public XifShader? Params { get; init; }

        /// <summary>Half the shape's size in item units, for a shader fitted to it; zero where there is no one shape.</summary>
        public Vector2 HalfSize { get; init; }
    }
}
