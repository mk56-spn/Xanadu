using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Lab.Experiments.Triangles;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Particles;

/// <summary>
/// The same flow of triangles, simulated one of two ways: DrawableTexture2D
/// blits feeding a triangle array, or a GPUParticles2D with a particle shader.
/// Both include one step function, so only the machinery differs.
/// </summary>
public sealed class ParticleExperiment(string name, string sourcePath, bool drawable, string[] notes) : ILabExperiment
{
    private const string Folder = "res://Experiments/Particles/";
    private const float StatsWidth = 580;

    private static readonly int[] Counts = [1 << 8, 1 << 10, 1 << 12, 1 << 14, 1 << 16];
    private static readonly int[] SubstepSteps = [1, 2, 4, 8, 16, 32, 64];
    private static int _countIndex = TriangleBench.StartIndex("--count=", Array.ConvertAll(Counts, c => (float)c), 2);
    private static int _substepIndex = TriangleBench.StartIndex("--substeps=", Array.ConvertAll(SubstepSteps, s => (float)s), 3);

    private LabStepper _countStepper = null!;
    private LabStepper _substepStepper = null!;
    private UiNode _stats;

    public static ParticleExperiment Drawable() => new(
        "PARTICLES · DRAWABLE",
        Folder + "particles_step.gdshader",
        drawable: true,
        [
            "State in DrawableTexture2D: one texel per particle.",
            "One blit_rect_multi a frame steps all of them.",
            "vertex() reads its texel and places the triangle.",
        ]);

    public static ParticleExperiment Godot() => new(
        "PARTICLES · GODOT",
        Folder + "particles_godot.gdshader",
        drawable: false,
        [
            "GPUParticles2D with a custom particle shader.",
            "Same step function, included from one file.",
            "Each particle is a quad folded into a triangle.",
        ]);

    public string Name => name;
    public string[] Notes => notes;
    public string? SourcePath => sourcePath;

    public float ControlsHeight =>
        UiTheme.ButtonHeight * 2 + UiNode.Measure("A", UiType.Small).Y * 2 + UiTheme.ContainerSpacing * 4;

    /// <summary>Particle count, shared by both tabs so they compare the same load.</summary>
    public static int Count => Counts[_countIndex];

    /// <summary>Flow evaluations per particle per frame, shared by both tabs: the step's cost knob.</summary>
    public static int Substeps => SubstepSteps[_substepIndex];

    public Node2D CreateStage(Rect2 area) => drawable
        ? new DrawableParticleStage(area, this, Load("particles_step"), Load("particles_draw"))
        : new GodotParticleStage(area, this, Load("particles_godot"), Load("particles_godot_draw"));

    public void BuildPanel(UiNode panel)
    {
        _countStepper = new LabStepper(panel, CountText());
        _substepStepper = new LabStepper(panel, SubstepText());
        _stats = panel.Text("measuring...", UiType.Small, Palette.Accent).Width(StatsWidth);
        panel.Text("Whole-frame times. Physics: particle_motion.gdshaderinc.", UiType.Small, Palette.TextDim)
            .Width(StatsWidth);
    }

    internal void Poll()
    {
        _countIndex = _countStepper.Clamped(_countIndex, Counts.Length);
        _substepIndex = _substepStepper.Clamped(_substepIndex, SubstepSteps.Length);
        _countStepper.SetText(CountText());
        _substepStepper.SetText(SubstepText());
    }

    internal void Show(string line) => _stats.SetText(line);

    private static Shader Load(string file) => GD.Load<Shader>(Folder + file + ".gdshader");

    private static string CountText() => $"COUNT {Count:N0}";

    private static string SubstepText() => $"SUBSTEPS {Substeps}";
}

/// <summary>
/// Feeds the step uniforms both versions share, rebuilds when the count changes
/// and reports frame times while shown.
/// </summary>
public abstract partial class ParticleStage : Node2D
{
    /// <summary>Longest step simulated in one frame, so a hitch doesn't fling everything.</summary>
    private const float MaxStep = 1f / 30f;

    private const float ReportSeconds = 0.5f;

    private static readonly bool PrintStats = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--print-stats");

    protected readonly Rect2 Area;
    protected int Count;
    private readonly ParticleExperiment _experiment = null!;
    private Vector2 _lastMouse;
    private float _time;

    private double _gpu;
    private double _cpu;
    private int _frames;
    private float _elapsed;

    protected ParticleStage() { }

    protected ParticleStage(Rect2 area, ParticleExperiment experiment)
    {
        Area = area;
        _experiment = experiment;
    }

    public override void _Ready() =>
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);

    public override void _Process(double delta)
    {
        if (_experiment is null || !IsVisibleInTree())
            return;

        _experiment.Poll();
        Vector2 mouse = LabHost.Mouse.Clamp(Area.Position, Area.End);
        if (ParticleExperiment.Count != Count)
        {
            Count = ParticleExperiment.Count;
            _lastMouse = mouse;
            Allocate();
        }

        float dt = Math.Min((float)delta, MaxStep);
        _time += dt;
        ShaderMaterial step = Step;
        step.SetShaderParameter("emitter", mouse);
        step.SetShaderParameter("emitter_velocity", (mouse - _lastMouse) / Math.Max(dt, 1e-4f));
        step.SetShaderParameter("dt", dt);
        step.SetShaderParameter("time", _time);
        step.SetShaderParameter("substeps", ParticleExperiment.Substeps);
        _lastMouse = mouse;

        Advance();
        Report(delta);
    }

    /// <summary>The material running the shared step function.</summary>
    protected abstract ShaderMaterial Step { get; }

    /// <summary>Starts <see cref="Count"/> particles from never-born.</summary>
    protected abstract void Allocate();

    /// <summary>Runs anything the step needs beyond its uniforms.</summary>
    protected virtual void Advance() { }

    protected static ShaderMaterial StepMaterial(Shader shader, Rect2 area)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("bounds", new Vector4(area.Position.X, area.Position.Y, area.End.X, area.End.Y));
        return material;
    }

    private void Report(double delta)
    {
        Rid viewport = GetViewport().GetViewportRid();
        _gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
        _cpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
        _frames++;
        _elapsed += (float)delta;
        if (_elapsed < ReportSeconds)
            return;

        string line = $"GPU {_gpu / _frames:0.000} ms   CPU {_cpu / _frames:0.000} ms   {Count:N0} × {ParticleExperiment.Substeps} STEPS";
        _experiment.Show(line);
        if (PrintStats)
            GD.Print($"[particles] {_experiment.Name}  {line}");
        _gpu = _cpu = 0;
        _frames = 0;
        _elapsed = 0;
    }
}

/// <summary>
/// Owns two pairs of state textures and swaps them every frame: the blit reads
/// the front pair and writes the back pair. Geometry is built once per count.
/// </summary>
public partial class DrawableParticleStage : ParticleStage
{
    /// <summary>State texture width; the height is the count over this.</summary>
    private const int Width = 256;

    private const float HalfRoot3 = 0.8660254f;
    private static readonly Color[] White = [Colors.White];

    private readonly ShaderMaterial _step = null!;
    private readonly DrawableTexture2D[] _motion = new DrawableTexture2D[2];
    private readonly DrawableTexture2D[] _life = new DrawableTexture2D[2];
    private int _front;
    private Rect2I _rect;
    private int[] _indices = [];
    private Vector2[] _points = [];
    private Vector2[] _texels = [];

    public DrawableParticleStage() { }

    public DrawableParticleStage(Rect2 area, ParticleExperiment experiment, Shader step, Shader draw)
        : base(area, experiment)
    {
        _step = StepMaterial(step, area);
        Material = new ShaderMaterial { Shader = draw };
    }

    protected override ShaderMaterial Step => _step;

    public override void _Ready()
    {
        base._Ready();
        // The geometry sits around the origin until vertex() moves it, so its own
        // bounds would get it culled; claim the whole field instead.
        RenderingServer.CanvasItemSetCustomRect(GetCanvasItem(), true, Area);
    }

    protected override void Advance()
    {
        int back = 1 - _front;
        _motion[back].BlitRectMulti(_rect, [_motion[_front], _life[_front]], [_life[back]], null, 0, _step);
        _front = back;
        var draw = (ShaderMaterial)Material;
        draw.SetShaderParameter("motion", _motion[_front]);
        draw.SetShaderParameter("life", _life[_front]);
    }

    /// <summary>Fresh, cleared state textures and one triangle per texel.</summary>
    protected override void Allocate()
    {
        _rect = new Rect2I(0, 0, Width, Count / Width);
        for (int i = 0; i < 2; i++)
        {
            _motion[i] = Cleared();
            _life[i] = Cleared();
        }
        _front = 0;

        _indices = new int[Count * 3];
        _points = new Vector2[Count * 3];
        _texels = new Vector2[Count * 3];
        var tip = new Vector2(1, 0);
        var upper = new Vector2(-0.5f, -HalfRoot3);
        var lower = new Vector2(-0.5f, HalfRoot3);
        for (int p = 0; p < Count; p++)
        {
            int v = p * 3;
            _points[v] = tip;
            _points[v + 1] = upper;
            _points[v + 2] = lower;
            var texel = new Vector2(p % Width, p / Width);
            for (int k = 0; k < 3; k++)
            {
                _indices[v + k] = v + k;
                _texels[v + k] = texel;
            }
        }
        QueueRedraw();
    }

    private DrawableTexture2D Cleared()
    {
        var texture = new DrawableTexture2D();
        texture.Setup(_rect.Size.X, _rect.Size.Y, DrawableTexture2D.DrawableFormat.Rgbaf, new Color(0, 0, 0, 0));
        return texture;
    }

    public override void _Draw()
    {
        if (_points.Length > 0)
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _indices, _points, White, _texels);
    }
}

/// <summary>
/// A GPUParticles2D running the shared step in its particle shader. Every
/// particle is emitted at once and Godot's own lifetime never runs out, so
/// birth, death and respawn are all the shader's, as in the blit version.
/// </summary>
public partial class GodotParticleStage : ParticleStage
{
    /// <summary>Godot's per-particle lifetime: long enough that it never restarts anyone.</summary>
    private const double NeverExpires = 100_000;

    private readonly ShaderMaterial _step = null!;
    private readonly GpuParticles2D _particles = null!;

    public GodotParticleStage() { }

    public GodotParticleStage(Rect2 area, ParticleExperiment experiment, Shader step, Shader draw)
        : base(area, experiment)
    {
        _step = StepMaterial(step, area);
        _particles = new GpuParticles2D
        {
            ProcessMaterial = _step,
            Material = new ShaderMaterial { Shader = draw },
            Lifetime = NeverExpires,
            Explosiveness = 1,
            FixedFps = 0,
            Interpolate = false,
            LocalCoords = false,
            Emitting = false,
            VisibilityRect = area,
        };
        AddChild(_particles);
    }

    protected override ShaderMaterial Step => _step;

    protected override void Allocate()
    {
        _particles.Amount = Count;
        _particles.Emitting = true;
        _particles.Restart();
    }
}
