using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Lab.Experiments.Triangles;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Fluid;

/// <summary>
/// A stable-fluids smoke sim in buffered blits: velocity, dye and pressure live in
/// ping-ponged DrawableTexture2Ds, and each frame runs advect, divergence, N Jacobi
/// pressure sweeps and projection before one canvas shader shows the result.
/// </summary>
public sealed class FluidExperiment : ILabExperiment
{
    public const string Folder = "res://Experiments/Fluid/";

    private static readonly int[] Scales = [2, 3, 4, 6, 8];
    private static readonly int[] IterationSteps = [1, 5, 10, 20, 40, 80];
    private static readonly int[] VorticitySteps = [0, 15, 30, 50, 80];
    private static readonly string[] Views = ["DYE", "VELOCITY", "PRESSURE"];

    private static int _scaleIndex = TriangleBench.StartIndex("--scale=", Array.ConvertAll(Scales, s => (float)s), 2);
    private static int _iterationIndex = TriangleBench.StartIndex("--iterations=", Array.ConvertAll(IterationSteps, s => (float)s), 3);
    private static int _vorticityIndex = TriangleBench.StartIndex("--vorticity=", Array.ConvertAll(VorticitySteps, s => (float)s), 2);
    private static int _view = TriangleBench.StartIndex("--view=", [0, 1, 2], 0);

    private LabStepper _scaleStepper = null!, _iterationStepper = null!, _vorticityStepper = null!, _viewStepper = null!;

    public string Name => "FLUID · BUFFERS";

    public string[] Notes =>
    [
        "Stable fluids in DrawableTexture2D ping-pong.",
        "Per frame: advect velocity + dye, divergence,",
        "N Jacobi pressure sweeps, subtract the gradient.",
        "Vorticity confinement puts the curls back.",
        "Move the pointer over the stage to stir it.",
    ];

    public string? SourcePath => Folder + "fluid_advect.gdshader";

    public float ControlsHeight => UiMetrics.Control.Md * 4 + UiTheme.ContainerSpacing * 4;

    /// <summary>Screen pixels per cell; the grid is the stage over this.</summary>
    public static int Scale => Scales[_scaleIndex];

    public static int Iterations => IterationSteps[_iterationIndex];

    public static float Vorticity => VorticitySteps[_vorticityIndex];

    public static int View => _view;

    public Node2D CreateStage(Rect2 area) => new FluidStage(area, this);

    public void BuildPanel(UiNode panel)
    {
        _scaleStepper = new LabStepper(panel);
        _iterationStepper = new LabStepper(panel);
        _vorticityStepper = new LabStepper(panel);
        _viewStepper = new LabStepper(panel);
        Refresh();
    }

    internal void Poll()
    {
        _scaleIndex = _scaleStepper.Clamped(_scaleIndex, Scales.Length);
        _iterationIndex = _iterationStepper.Clamped(_iterationIndex, IterationSteps.Length);
        _vorticityIndex = _vorticityStepper.Clamped(_vorticityIndex, VorticitySteps.Length);
        _view = _viewStepper.Wrapped(_view, Views.Length);
        Refresh();
    }

    private void Refresh()
    {
        _scaleStepper.SetText($"CELL {Scale} PX");
        _iterationStepper.SetText($"PRESSURE × {Iterations}");
        _vorticityStepper.SetText($"VORTICITY {Vorticity}");
        _viewStepper.SetText($"VIEW {Views[_view]}");
    }
}

/// <summary>Owns the buffers, runs the passes each frame and draws the chosen buffer over the stage.</summary>
public partial class FluidStage : Node2D
{
    /// <summary>Longest step simulated in one frame, so a hitch doesn't blow the flow up.</summary>
    private const float MaxStep = 1f / 30f;

    private static readonly StringName Dt = "dt", TimeName = "time", VorticityName = "vorticity",
        PointerFrom = "pointer_from", PointerTo = "pointer_to", PointerVelocity = "pointer_velocity",
        PointerOn = "pointer_on", Grid = "grid";

    private readonly Rect2 _area;
    private readonly FluidExperiment _experiment = null!;
    private readonly ShaderMaterial _advect = null!, _divergence = null!, _pressure = null!, _project = null!;
    private readonly ShaderMaterial _view = null!;
    private readonly DrawableTexture2D[] _velocity = new DrawableTexture2D[2];
    private readonly DrawableTexture2D[] _dye = new DrawableTexture2D[2];
    private readonly DrawableTexture2D[] _pressureField = new DrawableTexture2D[2];
    private ShaderQuad? _quad;
    private Rect2I _rect;
    private Vector2 _origin;
    private int _scale;
    private int _front;
    /// <summary>Dye flips once a frame, velocity twice (advect, project), so each keeps its own front.</summary>
    private int _dyeFront;
    private int _pressureFront;
    private float _time;
    private Vector2 _lastPointer;

    public FluidStage() { }

    public FluidStage(Rect2 area, FluidExperiment experiment)
    {
        _area = area;
        _experiment = experiment;
        _advect = Load("fluid_advect");
        _divergence = Load("fluid_divergence");
        _pressure = Load("fluid_pressure");
        _project = Load("fluid_project");
        _view = Load("fluid_view");
    }

    private static ShaderMaterial Load(string name) =>
        new() { Shader = GD.Load<Shader>(FluidExperiment.Folder + name + ".gdshader") };

    public override void _Process(double delta)
    {
        if (_experiment is null || !IsVisibleInTree())
            return;

        _experiment.Poll();
        if (FluidExperiment.Scale != _scale)
            Allocate(FluidExperiment.Scale);

        float dt = Math.Min((float)delta, MaxStep);
        _time += dt;
        Step(dt);
        Present();
    }

    /// <summary>Fresh, still buffers sized to the stage at <paramref name="scale"/> pixels per cell.</summary>
    private void Allocate(int scale)
    {
        _scale = scale;
        var cells = (Vector2I)(_area.Size / scale).Floor();
        _rect = new Rect2I(Vector2I.Zero, cells);
        _origin = _area.Position + (_area.Size - (Vector2)cells * scale) / 2;
        for (int i = 0; i < 2; i++)
        {
            _velocity[i] = Cleared(cells);
            _dye[i] = Cleared(cells);
            _pressureField[i] = Cleared(cells);
        }
        _front = _dyeFront = _pressureFront = 0;
        foreach (ShaderMaterial m in (ReadOnlySpan<ShaderMaterial>)[_advect, _divergence, _pressure, _project])
            m.SetShaderParameter(Grid, (Vector2)cells);

        _quad?.QueueFree();
        _quad = new ShaderQuad((Vector2)cells * scale) { Position = _origin, Material = _view };
        AddChild(_quad);
        _lastPointer = PointerCells();
    }

    private static DrawableTexture2D Cleared(Vector2I size)
    {
        var texture = new DrawableTexture2D();
        texture.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgbaf, new Color(0, 0, 0, 0));
        return texture;
    }

    private Vector2 PointerCells() => (LabHost.Mouse - _origin) / _scale;

    private void Step(float dt)
    {
        Vector2 pointer = PointerCells();
        bool over = _area.HasPoint(LabHost.Mouse);
        _advect.SetShaderParameter(Dt, dt);
        _advect.SetShaderParameter(TimeName, _time);
        _advect.SetShaderParameter(VorticityName, FluidExperiment.Vorticity);
        _advect.SetShaderParameter(PointerFrom, _lastPointer);
        _advect.SetShaderParameter(PointerTo, pointer);
        _advect.SetShaderParameter(PointerVelocity, (pointer - _lastPointer) / Math.Max(dt, 1e-4f));
        _advect.SetShaderParameter(PointerOn, over ? 1f : 0f);
        _lastPointer = pointer;

        int back = 1 - _front;
        int dyeBack = 1 - _dyeFront;
        _velocity[back].BlitRectMulti(_rect, [_velocity[_front], _dye[_dyeFront]], [_dye[dyeBack]], null, 0, _advect);
        _front = back;
        _dyeFront = dyeBack;

        BlitPressure([_velocity[_front], _pressureField[_pressureFront]], _divergence);
        for (int i = 0; i < FluidExperiment.Iterations; i++)
            BlitPressure([_pressureField[_pressureFront]], _pressure);

        back = 1 - _front;
        _velocity[back].BlitRectMulti(_rect, [_velocity[_front], _pressureField[_pressureFront]], [], null, 0, _project);
        _front = back;
    }

    private void BlitPressure(Godot.Collections.Array<Texture2D> sources, ShaderMaterial material)
    {
        int back = 1 - _pressureFront;
        _pressureField[back].BlitRectMulti(_rect, sources, [], null, 0, material);
        _pressureFront = back;
    }

    private void Present()
    {
        _view.SetShaderParameter("dye", _dye[_dyeFront]);
        _view.SetShaderParameter("velocity", _velocity[_front]);
        _view.SetShaderParameter("pressure", _pressureField[_pressureFront]);
        _view.SetShaderParameter("view", FluidExperiment.View);
    }
}
