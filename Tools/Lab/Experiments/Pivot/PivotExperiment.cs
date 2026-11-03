using Godot;

namespace Xanadu.Lab.Experiments.Pivot;

/// <summary>
/// Squares spinning about their own origin: at the centre, on a corner and well
/// outside the square. The shader marks the origin it reads from the matrices,
/// growing the quad to reach it; if the maths is right the mark holds still
/// while the square turns around it.
/// </summary>
public sealed class PivotExperiment : ILabExperiment
{
    private const string ShaderPath = "res://Experiments/Pivot/pivot_marker.gdshader";

    public string Name => "PIVOT";

    public string[] Notes =>
    [
        "The point an item turns around is its origin:",
        "(CANVAS_MATRIX * MODEL_MATRIX)[3].xy, in screen px.",
        "Compared against FRAGCOORD, so no uniforms.",
        "Outside the shape, vertex() grows the quad to",
        "reach it; the tint is the overdraw paid for.",
    ];

    public string? SourcePath => ShaderPath;

    public Node2D CreateStage(Rect2 area) => new PivotStage(area, GD.Load<Shader>(ShaderPath));
}

/// <summary>Three squares in a row, each turning about a different origin.</summary>
public partial class PivotStage : Node2D
{
    private static readonly Vector2 Size = new(220, 220);
    private readonly ShaderQuad[] _quads = [];
    private float _time;

    public PivotStage() { }

    public PivotStage(Rect2 area, Shader shader)
    {
        var material = new ShaderMaterial { Shader = shader };
        Vector2[] origins = [Size / 2, Vector2.Zero, new(-90, 110)];
        _quads = new ShaderQuad[origins.Length];
        float cell = area.Size.X / origins.Length;
        for (int i = 0; i < origins.Length; i++)
        {
            _quads[i] = new ShaderQuad(Size, origins[i])
            {
                Material = material,
                // The square's rest centre sits mid-cell, wherever its pivot is.
                Position = new Vector2(area.Position.X + cell * (i + 0.5f), area.GetCenter().Y)
                           + origins[i] - Size / 2,
            };
            AddChild(_quads[i]);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (ShaderQuad quad in _quads)
            quad.Rotation = _time * 0.5f;
    }
}
