using Godot;
using Xanadu.Ecs;

namespace Xanadu.Lab.Experiments.Triangles;

/// <summary>A grid of triangles pointing at the mouse, drawn one of two ways.</summary>
public sealed class TriangleExperiment(string name, string shaderPath, bool geometry, string[] notes) : ILabExperiment
{
    private readonly TriangleBench _bench = new();

    public string Name => name;
    public string[] Notes => notes;
    public string? SourcePath => shaderPath;
    public float ControlsHeight => TriangleBench.Height;

    public Node2D CreateStage(Rect2 area)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(shaderPath) };
        return geometry
            ? new GeometryTriangleStage(area, material, _bench)
            : new FragmentTriangleStage(area, material, _bench);
    }

    public void BuildPanel(UiNode panel) => _bench.BuildPanel(panel);
}

/// <summary>Rebuilds when the shared density changes and reports frame times while shown.</summary>
public abstract partial class TriangleStage : Node2D
{
    protected Rect2 Field;
    protected int Triangles;
    private readonly Rect2 _area;
    private readonly TriangleBench? _bench;
    private float _built = -1;
    private int _layers = -1;

    protected TriangleStage() { }

    protected TriangleStage(Rect2 area, ShaderMaterial material, TriangleBench bench)
    {
        _area = area;
        _bench = bench;
        Material = material;
    }

    public override void _Ready() =>
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);

    public override void _Process(double delta)
    {
        if (_bench is null || !IsVisibleInTree())
            return;

        _bench.Poll();
        Viewport viewport = GetViewport();
        if (viewport.Msaa2D != TriangleBench.Msaa)
            viewport.Msaa2D = TriangleBench.Msaa;
        float cell = TriangleBench.Cell;
        if (cell != _built)
        {
            _built = cell;
            Field = TriangleBench.FullCells(_area, cell);
            Vector2 count = (Field.Size / cell).Round();
            Triangles = (int)(count.X * count.Y);
            Rebuild(cell);
            QueueRedraw();
        }
        if (TriangleBench.Layers != _layers)
        {
            _layers = TriangleBench.Layers;
            QueueRedraw();
        }

        _bench.Report(GetViewport().GetViewportRid(), Triangles, delta);
    }

    protected abstract void Rebuild(float cell);
}

/// <summary>One quad over the field; the fragment shader finds the triangles.</summary>
public partial class FragmentTriangleStage : TriangleStage
{
    public FragmentTriangleStage() { }

    public FragmentTriangleStage(Rect2 area, ShaderMaterial material, TriangleBench bench)
        : base(area, material, bench) { }

    protected override void Rebuild(float cell) =>
        ((ShaderMaterial)Material).SetShaderParameter("cell", cell);

    public override void _Draw()
    {
        for (int i = 0; i < TriangleBench.Layers; i++)
            DrawRect(Field, Colors.White);
    }
}

/// <summary>Three vertices per triangle in one triangle array; the vertex shader turns them.</summary>
public partial class GeometryTriangleStage : TriangleStage
{
    private const float Size = 0.4f;
    private const float HalfRoot3 = 0.8660254f;

    private int[] _indices = [];
    private Vector2[] _points = [];
    private Vector2[] _centers = [];
    private static readonly Color[] White = [Colors.White];

    public GeometryTriangleStage() { }

    public GeometryTriangleStage(Rect2 area, ShaderMaterial material, TriangleBench bench)
        : base(area, material, bench) { }

    /// <summary>Lays every triangle out pointing along +x; UV carries its centre for the shader.</summary>
    protected override void Rebuild(float cell)
    {
        int columns = Mathf.RoundToInt(Field.Size.X / cell);
        _indices = new int[Triangles * 3];
        _points = new Vector2[Triangles * 3];
        _centers = new Vector2[Triangles * 3];
        float r = Size * cell;
        var tip = new Vector2(r, 0);
        var upper = new Vector2(-r / 2, -r * HalfRoot3);
        var lower = new Vector2(-r / 2, r * HalfRoot3);

        for (int t = 0; t < Triangles; t++)
        {
            Vector2 center = Field.Position + new Vector2(t % columns + 0.5f, t / columns + 0.5f) * cell;
            int v = t * 3;
            _points[v] = center + tip;
            _points[v + 1] = center + upper;
            _points[v + 2] = center + lower;
            for (int k = 0; k < 3; k++)
            {
                _indices[v + k] = v + k;
                _centers[v + k] = center;
            }
        }
    }

    public override void _Draw()
    {
        if (_points.Length == 0)
            return;

        for (int i = 0; i < TriangleBench.Layers; i++)
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _indices, _points, White, _centers);
    }
}
