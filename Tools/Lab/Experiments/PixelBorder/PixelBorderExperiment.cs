using Godot;

namespace Xanadu.Lab.Experiments.PixelBorder;

/// <summary>
/// Squares under every transform that should not change a border's pixel width:
/// size, non-uniform stretch, rotation and a pulsing scale. Each pair of tabs
/// runs the same stage with a different shader.
/// </summary>
public sealed class PixelBorderExperiment(string name, string shaderPath, string[] notes) : ILabExperiment
{
    public string Name => name;
    public string[] Notes => notes;
    public string? SourcePath => shaderPath;

    public Node2D CreateStage(Rect2 area) =>
        new PixelBorderStage(area, GD.Load<Shader>(shaderPath));
}

/// <summary>Lays the squares out on a grid in the stage area and animates them.</summary>
public partial class PixelBorderStage : Node2D
{
    private readonly Node2D[] _spinning = [];
    private readonly Node2D[] _pulsing = [];
    private float _time;

    public PixelBorderStage() { }

    public PixelBorderStage(Rect2 area, Shader shader)
    {
        var material = new ShaderMaterial { Shader = shader };
        Vector2 cell = area.Size / new Vector2(3, 2);
        Vector2 Center(int column, int row) => area.Position + cell * new Vector2(column + 0.5f, row + 0.5f);

        Square(material, Center(0, 0), new Vector2(60, 60));
        Square(material, Center(1, 0), new Vector2(240, 240));
        Square(material, Center(2, 0), new Vector2(360, 60));
        _spinning = [Square(material, Center(0, 1), new Vector2(200, 200))];
        _pulsing = [Square(material, Center(1, 1), new Vector2(100, 100))];

        // Same quad scaled by the node, not the rect: only the matrix knows.
        Node2D scaled = Square(material, Center(2, 1), new Vector2(10, 10));
        scaled.Scale = new Vector2(30, 12);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (Node2D node in _spinning)
            node.Rotation = _time * 0.6f;
        foreach (Node2D node in _pulsing)
            node.Scale = Vector2.One * (1.5f + Mathf.Sin(_time * 1.4f));
    }

    /// <summary>A pivot at <paramref name="center"/> holding a quad whose origin is its corner.</summary>
    private Node2D Square(Material material, Vector2 center, Vector2 size)
    {
        var pivot = new Node2D { Position = center };
        pivot.AddChild(new ShaderQuad(size) { Material = material, Position = -size / 2 });
        AddChild(pivot);
        return pivot;
    }
}
