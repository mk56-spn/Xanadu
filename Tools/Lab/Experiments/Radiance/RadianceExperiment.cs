using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Lab.Experiments.Triangles;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Radiance;

/// <summary>
/// 2D global illumination with radiance cascades, all in blits: a scene pass, a
/// jump-flood distance field, then one pass per cascade from the top down, each
/// merging into the one below. The light costs the same however many lamps there are.
/// </summary>
public sealed class RadianceExperiment : ILabExperiment
{
    public const string Folder = "res://Experiments/Radiance/";

    private static readonly int[] Scales = [1, 2, 4];
    private static readonly string[] Views = ["LIGHT", "DISTANCE", "SCENE"];

    private static int _scaleIndex = TriangleBench.StartIndex("--scale=", Array.ConvertAll(Scales, s => (float)s), 0);
    private static int _wanted = TriangleBench.StartIndex("--cascades=", [0, 1, 2, 3, 4, 5, 6, 7, 8], 8);
    private static int _view = TriangleBench.StartIndex("--view=", [0, 1, 2], 0);

    private LabStepper _scaleStepper = null!, _cascadeStepper = null!, _viewStepper = null!;

    public string Name => "LIGHT · CASCADES";

    public string[] Notes =>
    [
        "Radiance cascades: real bounce-free 2D GI.",
        "Jump flooding turns the scene into distances.",
        "Each cascade: probes 2x sparser, rays 4x denser,",
        "4x longer; same cost each. Top down, every ray",
        "that misses takes the light from the one above,",
        "after marching to each coarse probe (bilinear fix).",
        "Drop cascades to see light lose its reach.",
    ];

    public string? SourcePath => Folder + "rc_cascade.gdshader";

    public float ControlsHeight => UiMetrics.Control.Md * 3 + UiTheme.ContainerSpacing * 3;

    /// <summary>Screen pixels per cell.</summary>
    public static int Scale => Scales[_scaleIndex];

    /// <summary>Cascades the stage can build at this size; set by the stage.</summary>
    public static int Available { get; set; } = 1;

    /// <summary>Cascades in use: what was asked for, within what the size allows.</summary>
    public static int Cascades => Math.Clamp(_wanted, 1, Available);

    public static int View => _view;

    public Node2D CreateStage(Rect2 area) => new RadianceStage(area, this);

    public void BuildPanel(UiNode panel)
    {
        _scaleStepper = new LabStepper(panel);
        _cascadeStepper = new LabStepper(panel);
        _viewStepper = new LabStepper(panel);
        Refresh();
    }

    internal void Poll()
    {
        _scaleIndex = _scaleStepper.Clamped(_scaleIndex, Scales.Length);
        if (_cascadeStepper.Step() is var cascade and not 0)
            _wanted = Math.Clamp(Cascades + cascade, 1, Available);
        _view = _viewStepper.Wrapped(_view, Views.Length);
        Refresh();
    }

    private void Refresh()
    {
        _scaleStepper.SetText($"CELL {Scale} PX");
        _cascadeStepper.SetText($"CASCADES {Cascades} / {Available}");
        _viewStepper.SetText($"VIEW {Views[_view]}");
    }
}

/// <summary>Owns the scene, distance and cascade buffers and runs every pass each frame.</summary>
public partial class RadianceStage : Node2D
{
    /// <summary>Cascade 0's ray length in cells; the shader's interval0.</summary>
    private const float Interval0 = 2f;

    private static readonly StringName Grid = "grid", TimeName = "time", Pointer = "pointer",
        PointerOn = "pointer_on", Jump = "jump", Cascade = "cascade", Top = "top";

    private readonly Rect2 _area;
    private readonly RadianceExperiment _experiment = null!;
    private readonly ShaderMaterial _scene = null!, _view = null!;
    /// <summary>One material per pass, so each keeps its own jump or cascade index.</summary>
    private readonly List<ShaderMaterial> _jumps = [];
    private readonly List<ShaderMaterial> _cascadeMaterials = [];
    private DrawableTexture2D _sceneTexture = null!;
    private readonly DrawableTexture2D[] _field = new DrawableTexture2D[2];
    private readonly DrawableTexture2D[] _cascades = new DrawableTexture2D[2];
    private ShaderQuad? _quad;
    private Rect2I _rect;
    private Vector2 _origin;
    private int _scale;
    private float _time;

    public RadianceStage() { }

    public RadianceStage(Rect2 area, RadianceExperiment experiment)
    {
        _area = area;
        _experiment = experiment;
        _scene = Load("rc_scene");
        _view = Load("rc_view");
    }

    private static ShaderMaterial Load(string name) =>
        new() { Shader = GD.Load<Shader>(RadianceExperiment.Folder + name + ".gdshader") };

    public override void _Process(double delta)
    {
        if (_experiment is null || !IsVisibleInTree())
            return;

        _experiment.Poll();
        if (RadianceExperiment.Scale != _scale)
            Allocate(RadianceExperiment.Scale);

        _time += (float)delta;
        Render();
    }

    /// <summary>
    /// Sizes the grid for <paramref name="scale"/>: enough cascades for the top one to
    /// reach across the diagonal, and a grid that divides into the top cascade's tiles.
    /// </summary>
    private void Allocate(int scale)
    {
        _scale = scale;
        Vector2 fit = (_area.Size / scale).Floor();
        int count = 1;
        while (Interval0 * (MathF.Pow(4, count) - 1) / 3 < fit.Length())
            count++;
        int tiles = 1 << count;
        var cells = new Vector2I((int)fit.X / tiles * tiles, (int)fit.Y / tiles * tiles);
        RadianceExperiment.Available = count;

        _rect = new Rect2I(Vector2I.Zero, cells);
        _origin = _area.Position + (_area.Size - (Vector2)cells * scale) / 2;
        _sceneTexture = Cleared(cells);
        for (int i = 0; i < 2; i++)
        {
            _field[i] = Cleared(cells);
            _cascades[i] = Cleared(cells);
        }
        _jumps.Clear();
        for (int jump = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(cells.X, cells.Y)) / 2; jump >= 1; jump /= 2)
        {
            ShaderMaterial m = Load("rc_jump");
            m.SetShaderParameter(Jump, jump);
            _jumps.Add(m);
        }
        _cascadeMaterials.Clear();
        for (int n = 0; n < count; n++)
        {
            ShaderMaterial m = Load("rc_cascade");
            m.SetShaderParameter(Cascade, n);
            m.SetShaderParameter("interval0", Interval0);
            _cascadeMaterials.Add(m);
        }
        foreach (ShaderMaterial m in (ReadOnlySpan<ShaderMaterial>)[_scene, _view, .. _jumps, .. _cascadeMaterials])
            m.SetShaderParameter(Grid, (Vector2)cells);

        _quad?.QueueFree();
        _quad = new ShaderQuad((Vector2)cells * scale) { Position = _origin, Material = _view };
        AddChild(_quad);
    }

    private static DrawableTexture2D Cleared(Vector2I size)
    {
        var texture = new DrawableTexture2D();
        texture.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgbaf, new Color(0, 0, 0, 0));
        return texture;
    }

    private void Render()
    {
        _scene.SetShaderParameter(TimeName, _time);
        _scene.SetShaderParameter(Pointer, (LabHost.Mouse - _origin) / _scale);
        _scene.SetShaderParameter(PointerOn, _area.HasPoint(LabHost.Mouse) ? 1f : 0f);
        // The scene reads nothing, but a blit needs a source; any spare buffer will do.
        _sceneTexture.BlitRectMulti(_rect, [_cascades[1]], [_field[0]], null, 0, _scene);

        // Jump flood: halve the reach each pass, ping-ponging the nearest-seed field.
        int front = 0;
        foreach (ShaderMaterial jump in _jumps)
        {
            _field[1 - front].BlitRectMulti(_rect, [_field[front]], [], null, 0, jump);
            front = 1 - front;
        }

        // Cascades from the top down; cascade n lands in buffer n % 2, so 0 ends in buffer 0.
        int top = RadianceExperiment.Cascades - 1;
        for (int n = top; n >= 0; n--)
        {
            ShaderMaterial cascade = _cascadeMaterials[n];
            cascade.SetShaderParameter(Top, top);
            _cascades[n & 1].BlitRectMulti(_rect, [_sceneTexture, _field[front], _cascades[(n + 1) & 1]], [], null, 0, cascade);
        }

        _view.SetShaderParameter("scene", _sceneTexture);
        _view.SetShaderParameter("field", _field[front]);
        _view.SetShaderParameter("cascade0", _cascades[0]);
        _view.SetShaderParameter("view", RadianceExperiment.View);
    }
}
