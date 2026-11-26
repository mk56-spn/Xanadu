using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xanadu.Core.Items;
using Xanadu.Core.Rendering;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Model.Animation;

/// <summary>
/// Xif nodes drawn as copies on the GPU (Docs/xif-modules.md, "Drawing on the render graph"): each node added is a
/// copy (one per copy of its instancer) of its geometry's mesh, kept on the GPU in the node's own space, placed, tinted
/// and given its own shader values by its copy alone. <see cref="Draw"/> draws every node sharing a shader and a
/// geometry in one draw, in the order they were added, the groups in the order they were first added. A node with no
/// common shader isn't copied: <see cref="XifDrawer"/> draws it at once.
/// </summary>
public sealed class XifBatch
{
    /// <summary>Keeps a copy's place across from reaching 1, so its whole part stays the copy's number (as XifDrawer's uv).</summary>
    private const float AcrossTop = 0.999f;

    /// <summary>The shader value an instancer's seed goes in.</summary>
    private const string Seed = "xif_seed";

    private readonly ConditionalWeakTable<ArtMesh, CanvasMesh> _meshes = [];
    private readonly Dictionary<string, MaterialParam[]> _described = [];
    private readonly Dictionary<(string Shader, ArtMesh Mesh), Group> _groups = [];
    private readonly List<Group> _order = [];
    private readonly List<XifInstance> _instances = [];

    /// <summary>Adds <paramref name="node"/>'s geometry placed by <paramref name="placement"/> (node space to target), <paramref name="unit"/> target pixels a node unit.</summary>
    public void Add(IRectCanvas canvas, Handle target, Xif node, Transform2D placement, float unit, XifTint tint)
    {
        if (node.Get<XifGeometry>() is not { } geometry)
            return;
        string shader = XifDrawer.ShaderOf(node);
        if (shader.Length == 0)
        {
            XifDrawer.Shared.DrawNode(canvas, target, node, placement, unit, tint);
            return;
        }

        ArtMesh art = XifDrawer.Shared.MeshOf(geometry);
        if (art.Triangles.Length == 0)
            return;

        if (!_groups.TryGetValue((shader, art), out Group? group))
        {
            group = new Group(shader, _meshes.GetValue(art, MeshOf));
            _groups[(shader, art)] = group;
        }
        if (group.Copies.Count == 0)
            _order.Add(group);

        int values = Values(group, node, art.Bounds.Size / 2);
        _instances.Clear();
        XifInstancer? instancer = node.Get<XifInstancer>();
        if (instancer is not null)
            XifInstances.Place(instancer, _instances);
        else
            _instances.Add(new XifInstance(0, Vector2.Zero, 1f, 0f));

        foreach (XifInstance copy in _instances)
        {
            float sin = MathF.Sin(copy.Turn) * copy.Scale, cos = MathF.Cos(copy.Turn) * copy.Scale;
            var place = new Transform2D(new Vector2(cos, sin), new Vector2(-sin, cos), copy.Offset);
            group.Copies.Add(new CanvasCopy(placement * place, tint.Modulate, tint.Flash, tint.FlashAmount, unit, copy.Index, values));
        }
    }

    /// <summary>Draws everything added since the last draw, a draw per shader and geometry, and starts again.</summary>
    public void Draw(IRectCanvas canvas, Handle target)
    {
        foreach (Group group in _order)
        {
            canvas.SelectCommonShader(group.Shader);
            canvas.DrawCopies(target, group.Mesh, CollectionsMarshal.AsSpan(group.Copies), CollectionsMarshal.AsSpan(group.Values));
            group.Copies.Clear();
            group.Values.Clear();
        }
        canvas.SelectCommonShader("");
        _order.Clear();
    }

    /// <summary>
    /// The node's values for its shader, one per value it declares in its order: the node's own, the instancer's seed,
    /// a [Fit] value's x and y from the shape's half size, else the shader's default. Returns where they start.
    /// </summary>
    private int Values(Group group, Xif node, Vector2 half)
    {
        int at = group.Values.Count;
        XifShader? module = node.Get<XifShader>();
        uint seed = node.Get<XifInstancer>()?.Seed ?? 0u;
        foreach (MaterialParam param in Described(group.Shader))
        {
            Vector4 value = param.Default;
            if (module?.Params.TryGetValue(param.Name, out float[]? set) == true && set.Length > 0)
                value = new Vector4(set[0], set.Length > 1 ? set[1] : 0f, set.Length > 2 ? set[2] : 0f, set.Length > 3 ? set[3] : 0f);
            if (param.Name == Seed)
                value = new Vector4(seed, 0f, 0f, 0f);
            if (param.Fit && module is { Fit: true } && half != Vector2.Zero)
                value = value with { X = half.X, Y = half.Y };
            group.Values.Add(value);
        }

        return at;
    }

    /// <summary>The values a shader declares, as the host's compiler reports them; kept per shader.</summary>
    private MaterialParam[] Described(string shader)
    {
        if (_described.TryGetValue(shader, out MaterialParam[]? known))
            return known;
        IReadOnlyList<MaterialParam>? found = Xanadu.Ui.Systems.HostPlatform.DescribeMaterial?.Invoke(new CanvasMaterial(shader));
        return _described[shader] = found is null ? [] : [.. found];
    }

    /// <summary>A geometry's triangles as a mesh in the node's space, uv across its box.</summary>
    private static CanvasMesh MeshOf(ArtMesh art)
    {
        int count = art.Triangles.Length;
        var points = new Vector2[count];
        var colors = new Color[count];
        var uvs = new Vector2[count];
        var curves = new byte[count / 3];
        Vector2 size = new(MathF.Max(art.Bounds.Size.X, 1e-3f), MathF.Max(art.Bounds.Size.Y, 1e-3f));
        for (int k = 0; k < count; k++)
        {
            int index = art.Triangles[k];
            Vector2 point = art.Points[index];
            points[k] = point;
            colors[k] = art.Colors[index];
            Vector2 uv = (point - art.Bounds.Position) / size;
            uvs[k] = new Vector2(Math.Clamp(uv.X, 0f, AcrossTop), uv.Y);
            if (k % 3 == 0)
                curves[k / 3] = art.CurveAt(k);
        }

        return new CanvasMesh(points, colors, uvs, curves);
    }

    /// <summary>One shader and geometry's copies this frame, and their values (each copy's from its <see cref="CanvasCopy.Values"/>).</summary>
    private sealed class Group(string shader, CanvasMesh mesh)
    {
        public readonly string Shader = shader;
        public readonly CanvasMesh Mesh = mesh;
        public readonly List<CanvasCopy> Copies = [];
        public readonly List<Vector4> Values = [];
    }
}
