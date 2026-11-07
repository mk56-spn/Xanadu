using Godot;

namespace Xanadu.Lab.Experiments.Aero;

/// <summary>
/// A Frutiger Aero glass button drawn entirely by a fragment shader, over a
/// backdrop drawn by another. The button refracts the screen behind it, so it
/// does not depend on the sky. The stage only eases the hover and press state
/// and remembers the last click for the ripple.
/// </summary>
public sealed class AeroButtonExperiment : ILabExperiment
{
    private const string SkyPath = "res://Experiments/Aero/aero_sky.gdshader";
    private const string ButtonPath = "res://Experiments/Aero/aero_button.gdshader";

    public string Name => "AERO";

    public string[] Notes =>
    [
        "Two quads: the sky, then the button over it.",
        "No textures; the glass refracts the screen,",
        "gloss, glow, bubbles and label are all SDFs.",
        "Hover it, press it.",
    ];

    public string? SourcePath => ButtonPath;

    public Node2D CreateStage(Rect2 area) =>
        new AeroButtonStage(area, GD.Load<Shader>(SkyPath), GD.Load<Shader>(ButtonPath));
}

/// <summary>The sky quad, the button quad above it, and the button's eased state.</summary>
public partial class AeroButtonStage : Node2D
{
    private static readonly Vector2 HalfSize = new(270, 78);
    /// <summary>Room around the pill for its shadow and hover halo.</summary>
    private const float Pad = 80;
    private const float HoverRate = 10f;
    private const float PressRate = 22f;

    private readonly ShaderMaterial _button = null!;
    private readonly Vector2 _center;
    private float _hover;
    private float _press;
    private bool _wasDown;
    private float _rippleAge = 10f;

    public AeroButtonStage() { }

    public AeroButtonStage(Rect2 area, Shader sky, Shader button)
    {
        var skyMaterial = new ShaderMaterial { Shader = sky };
        skyMaterial.SetShaderParameter("size", area.Size);
        AddChild(new ShaderQuad(area.Size) { Material = skyMaterial, Position = area.Position });

        _center = area.GetCenter() - new Vector2(0, area.Size.Y * 0.08f);
        _button = new ShaderMaterial { Shader = button };
        _button.SetShaderParameter("half_size", HalfSize);
        Vector2 padded = HalfSize + new Vector2(Pad, Pad);
        AddChild(new ShaderQuad(padded * 2, padded) { Material = _button, Position = _center });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector2 local = LabHost.Mouse - _center;
        bool over = PillDistance(local) <= 0;
        bool down = over && Input.IsMouseButtonPressed(MouseButton.Left);

        if (down && !_wasDown)
        {
            _rippleAge = 0;
            _button.SetShaderParameter("ripple_at", local);
        }
        _wasDown = down;
        _rippleAge += dt;

        _hover = Mathf.Lerp(_hover, over ? 1f : 0f, 1f - Mathf.Exp(-HoverRate * dt));
        _press = Mathf.Lerp(_press, down ? 1f : 0f, 1f - Mathf.Exp(-PressRate * dt));
        _button.SetShaderParameter("hover", _hover);
        _button.SetShaderParameter("press", _press);
        _button.SetShaderParameter("ripple_age", _rippleAge);
    }

    /// <summary>The shader's pill distance, so hover matches the drawn edge.</summary>
    private static float PillDistance(Vector2 p)
    {
        float r = Mathf.Min(HalfSize.X, HalfSize.Y);
        Vector2 q = p.Abs() - HalfSize + new Vector2(r, r);
        return q.Max(Vector2.Zero).Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0) - r;
    }
}
