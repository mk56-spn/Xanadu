using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Lab.Experiments.Triangles;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Rts;

/// <summary>
/// Five factions of eleven unit types fighting over rocky ground, entirely in
/// DrawableTexture2D state. Each spawner raises certain types, and a faction
/// fields more units the more spawners it holds. Armies start knowing only
/// their own base: they scout sectors and find spawners as they go. Row 0 of
/// the state is a control row of faction totals, spawners, every squad's goal
/// and its formation leader; each goal has a flow field, settled once at
/// startup, that routes squads to it.
/// </summary>
public sealed class RtsExperiment : ILabExperiment
{
    private const string Folder = "res://Experiments/Rts/";
    private const float StatsWidth = 580;

    /// <summary>Whole rows of 64, and a multiple of five so the factions are even.</summary>
    private static readonly int[] Counts = [1280, 2560, 5120, 10240, 20480, 40960, 81920, 102400];
    private static int _countIndex = TriangleBench.StartIndex("--units=", Array.ConvertAll(Counts, c => (float)c), 3);

    /// <summary><c>--scan=all</c> starts with the brute-force scan.</summary>
    private static bool _grid = !Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--scan=all");

    private LabStepper _countStepper = null!;
    private UiNode _stats;
    private UiNode _gpuPasses;
    private UiNode _gpuTotal;
    private UiNode _scanLabel;
    private Entity _scan;
    private bool _scanHeld;

    public string Name => "RTS · DRAWABLE";

    public string[] Notes =>
    [
        "Scout rifle heavy sniper artillery medic",
        "berserker guardian assassin commander.",
        "40 spawners; each core's shape is what it raises:",
        "hexagon base, square factory, triangle firebase,",
        "diamond barracks, circle command. 12 squads each.",
    ];

    public string? SourcePath => Folder + "rts_step.gdshader";

    public float ControlsHeight =>
        UiTheme.ButtonHeight * 2 + UiNode.Measure("A", UiType.Small).Y * 6 + UiTheme.ContainerSpacing * 8;

    /// <summary>Units across all factions.</summary>
    public static int Count => Counts[_countIndex];

    /// <summary>Scan through the spatial grid, rather than every unit.</summary>
    public static bool Grid => _grid;

    public Node2D CreateStage(Rect2 area) => new RtsStage(area, this,
        GD.Load<Shader>(Folder + "rts_step.gdshader"),
        GD.Load<Shader>(Folder + "rts_units.gdshader"),
        GD.Load<Shader>(Folder + "rts_hud.gdshader"),
        GD.Load<Shader>(Folder + "rts_keys.gdshader"),
        GD.Load<Shader>(Folder + "rts_sort.gdshader"),
        GD.Load<Shader>(Folder + "rts_cells.gdshader"),
        GD.Load<Shader>(Folder + "rts_terrain.gdshader"),
        GD.Load<Shader>(Folder + "rts_nav.gdshader"),
        GD.Load<Shader>(Folder + "rts_ground.gdshader"),
        GD.Load<Shader>(Folder + "rts_tally.gdshader"),
        GD.Load<Shader>(Folder + "rts_fire_keys.gdshader"),
        GD.Load<Shader>(Folder + "rts_intel.gdshader"));

    public void BuildPanel(UiNode panel)
    {
        _countStepper = new LabStepper(panel, CountText());
        UiNode scanRow = panel.Row().Gap(UiMetrics.Space.Sm);
        _scanLabel = scanRow.Text(ScanText(), UiType.Label, Palette.Text).Width(220);
        _scan = scanRow.Button("SWITCH", UiType.Label).Size(UiMetrics.Control.Md * 2 + UiMetrics.Space.Sm, UiMetrics.Control.Md);
        _stats = panel.Text("", UiType.Small, Palette.Accent).Width(StatsWidth);
        _gpuPasses = panel.Text("GPU ms/frame: waiting for timestamps", UiType.Small, Palette.Accent).Width(StatsWidth);
        _gpuTotal = panel.Text("", UiType.Small, Palette.Accent).Width(StatsWidth);
        panel.Text("Bars: units alive, kills, share of spawners", UiType.Small, Palette.TextDim)
            .Width(StatsWidth);
        panel.Text("against the leader. Diamonds mark army centres.", UiType.Small, Palette.TextDim)
            .Width(StatsWidth);
        panel.Text("Wheel zooms, drag pans, F fits, 1-5 jumps to an army.", UiType.Small, Palette.TextDim)
            .Width(StatsWidth);
    }

    internal void Poll()
    {
        _countIndex = _countStepper.Clamped(_countIndex, Counts.Length);
        if (LabStepper.Pressed(_scan, ref _scanHeld))
            _grid = !_grid;
        _countStepper.SetText(CountText());
        _scanLabel.SetText(ScanText());
    }

    internal void Show(string line) => _stats.SetText(line);

    internal void ShowGpu(string passes, string total)
    {
        _gpuPasses.SetText(passes);
        _gpuTotal.SetText(total);
    }

    private static string CountText() => $"UNITS {Count:N0}";

    private static string ScanText() => _grid ? "SCAN GRID" : "SCAN ALL";
}

/// <summary>
/// The battle on a field ten times the stage, seen through a camera. Three
/// pairs of state textures swap every frame: one blit_rect_multi reads the
/// front three and writes the back three. Before it, the grid is rebuilt:
/// keys, a bitonic sort, then the cell table. This node clips to the stage;
/// the world layer under it takes the camera, the scoreboard does not.
/// </summary>
public partial class RtsStage : Node2D
{
    /// <summary>State texture width; must match WIDTH in rts_rules.gdshaderinc.</summary>
    private const int Width = 64;

    private const int Factions = 5;

    /// <summary>The field is this many stages across, each way.</summary>
    private const float WorldScale = 50;

    /// <summary>Vertices per unit: body quad and tracer strip, two triangles each.</summary>
    private const int UnitVertices = 12;

    /// <summary>Scoreboard parts per faction (track, alive, kills, marker, spawners), six vertices each.</summary>
    private const int HudParts = 5;

    /// <summary>Grid cell size; must match CELL in rts_grid.gdshaderinc.</summary>
    private const float Cell = 160;

    /// <summary>Nav cell size; must match NAV_CELL in rts_rules.gdshaderinc.</summary>
    private const float NavCell = 96;

    /// <summary>Flow-field tiles, one per goal (40 spawners, 24 sector waypoints), in rows of eight; must match NAV_COLUMNS and GOALS in rts_rules.</summary>
    private const int NavColumns = 8;
    private const int NavRows = 8;

    /// <summary>One intel texel per goal; must cover GOALS in rts_rules.</summary>
    private const int Goals = 64;
    private const int Sectors = 24;

    /// <summary>
    /// Relaxation passes a step while the fields settle: each spreads them one
    /// cell. Even, so the latest field always ends up back in the same texture.
    /// </summary>
    private const int NavPasses = 32;

    /// <summary>
    /// Passes after which the fields have settled: more than the longest way
    /// round the rocks, in cells. Goals and rock never move, so relaxation
    /// stops there; until then, units head straight for goals not yet reached.
    /// </summary>
    private const int NavSettlePasses = 1536;

    /// <summary>The simulation's fixed step; drawing blends between steps.</summary>
    private const float StepSeconds = 1f / 30f;

    /// <summary>Steps a slow frame may catch up; past that the battle slows down instead.</summary>
    private const int MaxStepsPerFrame = 2;
    private const float ZoomStep = 1.15f;
    private const float MaxZoom = 4;

    private static readonly bool CheckGrid = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--check-grid");

    /// <summary><c>--check-howitzers</c>: every ten seconds, print each faction's living howitzers, shells in the air, sniper shots this step, and spawners known and sectors scouted.</summary>
    private static readonly bool CheckHowitzers = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--check-howitzers");

    /// <summary>Howitzers per faction and one rank in how many; must match rts_rules.</summary>
    private const int Howitzers = 16;
    private const int HowitzerEvery = 64;

    private readonly Rect2 _area;
    private readonly Rect2 _world;
    private readonly RtsExperiment _experiment = null!;
    private readonly ShaderMaterial _step = null!;
    private readonly ShaderMaterial _keysMaterial = null!;
    private readonly ShaderMaterial _sortMaterial = null!;
    private readonly ShaderMaterial _fireKeysMaterial = null!;
    private readonly ShaderMaterial _intelMaterial = null!;
    private readonly DrawableTexture2D[] _intel = new DrawableTexture2D[2];
    private int _intelFront;
    private readonly ShaderMaterial _cellsMaterial = null!;
    private readonly ShaderMaterial _unitsMaterial = null!;
    private readonly ShaderMaterial _hudMaterial = null!;
    private readonly ShaderMaterial _terrainMaterial = null!;
    private readonly ShaderMaterial _maskMaterial = null!;
    private readonly ShaderMaterial _navMaterial = null!;
    private readonly ShaderMaterial _groundMaterial = null!;
    private readonly ShaderMaterial _tallyMaterial = null!;
    private DrawableTexture2D _tally = null!;
    private Rect2I _tallyRect;
    private double _accumulator;
    private readonly Node2D _camera = null!;
    private readonly TriangleLayer _units = null!;
    private readonly int _cells;
    private readonly DrawableTexture2D[][] _state = [new DrawableTexture2D[2], new DrawableTexture2D[2], new DrawableTexture2D[2]];
    private readonly DrawableTexture2D[] _keys = new DrawableTexture2D[2];
    private DrawableTexture2D _grid = null!;
    private readonly DrawableTexture2D[] _fire = new DrawableTexture2D[2];
    private Rect2I _fireRect;
    private int _fireSlots;
    private readonly DrawableTexture2D[] _nav = new DrawableTexture2D[2];
    private readonly Rect2I _navRect;
    private int _navFront;
    private int _navPassesRun;
    private int _front;
    private int _count;
    private int _slots;
    private Rect2I _rect;
    private Rect2I _keysRect;
    private Rect2I _gridRect;
    private int _checkFrames;
    private int _howitzerSteps;
    private float _time;
    private readonly GpuPassTimer _gpu = new("rts:");
    private double _drawMs;
    private int _drawFrames;
    private double _reportElapsed;

    /// <summary>Seconds of frames averaged into each GPU report.</summary>
    private const double ReportSeconds = 1;

    private static readonly bool PrintStats = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--print-stats");

    private int _followFrames;
    private bool _dragging;
    private Vector2 _dragFrom;

    public RtsStage() { }

    public RtsStage(Rect2 area, RtsExperiment experiment, Shader step, Shader units, Shader hud,
        Shader keys, Shader sort, Shader cells, Shader terrain, Shader nav, Shader ground, Shader tally, Shader fireKeys, Shader intel)
    {
        _area = area;
        _world = new Rect2(Vector2.Zero, area.Size * WorldScale);
        _experiment = experiment;
        var bounds = new Vector4(_world.Position.X, _world.Position.Y, _world.End.X, _world.End.Y);
        _step = new ShaderMaterial { Shader = step };
        _step.SetShaderParameter("bounds", bounds);
        _keysMaterial = new ShaderMaterial { Shader = keys };
        _keysMaterial.SetShaderParameter("bounds", bounds);
        _sortMaterial = new ShaderMaterial { Shader = sort };
        _fireKeysMaterial = new ShaderMaterial { Shader = fireKeys };
        _intelMaterial = new ShaderMaterial { Shader = intel };
        _intelMaterial.SetShaderParameter("bounds", bounds);
        _fireKeysMaterial.SetShaderParameter("bounds", bounds);
        _cellsMaterial = new ShaderMaterial { Shader = cells };
        _cells = Mathf.CeilToInt(_world.Size.X / Cell) * Mathf.CeilToInt(_world.Size.Y / Cell);
        _terrainMaterial = new ShaderMaterial { Shader = terrain };
        _terrainMaterial.SetShaderParameter("bounds", bounds);
        _maskMaterial = new ShaderMaterial { Shader = terrain };
        _maskMaterial.SetShaderParameter("bounds", bounds);
        _maskMaterial.SetShaderParameter("masking", true);
        _tallyMaterial = new ShaderMaterial { Shader = tally };
        _navMaterial = new ShaderMaterial { Shader = nav };
        _navMaterial.SetShaderParameter("bounds", bounds);
        _navRect = new Rect2I(0, 0,
            Mathf.CeilToInt(_world.Size.X / NavCell) * NavColumns, Mathf.CeilToInt(_world.Size.Y / NavCell) * NavRows);

        // Draw nothing of our own, but clip everything under us to the stage.
        // The camera carries the ground and the units; the scoreboard stays put.
        ClipChildren = ClipChildrenMode.Only;
        _camera = new Node2D();
        AddChild(_camera);
        _groundMaterial = new ShaderMaterial { Shader = ground };
        _groundMaterial.SetShaderParameter("bounds", bounds);
        _camera.AddChild(new GroundLayer(_groundMaterial, _world));
        _unitsMaterial = new ShaderMaterial { Shader = units };
        _units = new TriangleLayer(_unitsMaterial, _world);
        _camera.AddChild(_units);
        _hudMaterial = new ShaderMaterial { Shader = hud };
        _hudMaterial.SetShaderParameter("origin", area.Position + new Vector2(20, 20));
        AddChild(new TriangleLayer(_hudMaterial, area, HudGeometry()));
        Fit();

        // --zoom=N starts zoomed, about the stage centre or --zoom-at=x,y (canvas px), for repeatable shots.
        Vector2 anchor = area.GetCenter();
        float? start = null;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--zoom=", StringComparison.Ordinal) && float.TryParse(arg["--zoom=".Length..], out float zoom))
                start = zoom;
            else if (arg.StartsWith("--zoom-at=", StringComparison.Ordinal)
                     && arg["--zoom-at=".Length..].Split(',') is [var x, var y]
                     && float.TryParse(x, out float ax) && float.TryParse(y, out float ay))
                anchor = new Vector2(ax, ay);
        }
        if (start is { } z)
            ZoomAt(anchor, z / Zoom);
    }

    /// <summary><c>--follow=N</c>: keep the camera on army N (1-5), for shots of the fighting.</summary>
    private static readonly int Follow = Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--follow=", StringComparison.Ordinal))
        is { } follow && int.TryParse(follow["--follow=".Length..], out int army) ? army : 0;

    /// <summary>Screenshot runs ignore the camera controls, so stray input can't move the shot.</summary>
    private static readonly bool Shooting = Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--shot=", StringComparison.Ordinal));

    /// <summary>The camera: world px to canvas px.</summary>
    private float Zoom => _camera.Scale.X;

    private float MinZoom => Math.Min(_area.Size.X / _world.Size.X, _area.Size.Y / _world.Size.Y);

    public override void _Draw() => DrawRect(_area, Colors.White);

    public override void _Ready() =>
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);

    public override void _Process(double delta)
    {
        if (_experiment is null || !IsVisibleInTree())
            return;

        _experiment.Poll();
        if (RtsExperiment.Count != _count)
            Allocate(RtsExperiment.Count);

        Pan();
        // Fixed-rate steps; a slow frame catches up at most MaxStepsPerFrame.
        _accumulator = Math.Min(_accumulator + delta, StepSeconds * MaxStepsPerFrame);
        while (_accumulator >= StepSeconds)
        {
            Simulate(StepSeconds);
            _accumulator -= StepSeconds;
        }
        _gpu.Collect();
        ReportGpu(delta);
        if (Follow > 0 && ++_followFrames % 15 == 0)
            JumpTo(Follow - 1);

        // After a step the back pair holds the step before, so drawing can blend.
        _unitsMaterial.SetShaderParameter("motion", _state[0][_front]);
        _unitsMaterial.SetShaderParameter("unit", _state[1][_front]);
        _unitsMaterial.SetShaderParameter("fx", _state[2][_front]);
        _unitsMaterial.SetShaderParameter("motion_prev", _state[0][1 - _front]);
        _unitsMaterial.SetShaderParameter("fx_prev", _state[2][1 - _front]);
        _unitsMaterial.SetShaderParameter("blend", (float)(_accumulator / StepSeconds));
        _hudMaterial.SetShaderParameter("motion", _state[0][_front]);
        _hudMaterial.SetShaderParameter("control", _state[2][_front]);
        _groundMaterial.SetShaderParameter("control", _state[2][_front]);
        _hudMaterial.SetShaderParameter("view", new Vector3(_camera.Position.X, _camera.Position.Y, Zoom));
        _experiment.Show($"ZOOM {Zoom:0.00}x   {_count:N0} UNITS");
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || Shooting)
            return;

        Vector2 mouse = GetGlobalMousePosition();
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } when _area.HasPoint(mouse):
                ZoomAt(mouse, ZoomStep);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } when _area.HasPoint(mouse):
                ZoomAt(mouse, 1 / ZoomStep);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle or MouseButton.Right } button:
                _dragging = button.Pressed && _area.HasPoint(mouse);
                _dragFrom = mouse;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.F }:
                Fit();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: >= Key.Key1 and <= Key.Key5 } key:
                JumpTo((int)(key.Keycode - Key.Key1));
                break;
        }
    }

    /// <summary>Drags move the world by however far the pointer went since last frame.</summary>
    private void Pan()
    {
        if (!_dragging)
            return;
        Vector2 mouse = GetGlobalMousePosition();
        _camera.Position += mouse - _dragFrom;
        _dragFrom = mouse;
        Clamp();
    }

    /// <summary>Scales about <paramref name="anchor"/>, so the world point under the pointer stays put.</summary>
    private void ZoomAt(Vector2 anchor, float factor)
    {
        float zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Vector2 world = (anchor - _camera.Position) / Zoom;
        _camera.Scale = new Vector2(zoom, zoom);
        _camera.Position = anchor - world * zoom;
        Clamp();
    }

    /// <summary>
    /// Centres army <paramref name="faction"/> from its control-row texel. A
    /// readback stalls for the GPU, so it only happens when asked.
    /// </summary>
    private void JumpTo(int faction)
    {
        Color army = _state[0][_front].GetImage().GetPixel(faction, 0);
        if (army.B <= 0)
            return;
        _camera.Position = _area.GetCenter() - new Vector2(army.R, army.G) * Zoom;
        Clamp();
    }

    /// <summary>The whole field, centred in the stage.</summary>
    private void Fit()
    {
        float zoom = MinZoom;
        _camera.Scale = new Vector2(zoom, zoom);
        _camera.Position = _area.GetCenter() - _world.GetCenter() * zoom;
    }

    /// <summary>Keeps some of the field on screen: its edges can't pass the stage's middle.</summary>
    private void Clamp()
    {
        Vector2 size = _world.Size * Zoom;
        Vector2 centre = _area.GetCenter();
        _camera.Position = _camera.Position.Clamp(centre - size, centre);
    }

    private void Simulate(float dt)
    {
        _time += dt;
        _gpu.Begin();
        _step.SetShaderParameter("dt", dt);
        _step.SetShaderParameter("time", _time);
        _step.SetShaderParameter("use_grid", RtsExperiment.Grid);

        Relax();
        _gpu.Mark("nav");
        if (RtsExperiment.Grid)
            BuildGrid();
        _step.SetShaderParameter("nav_field", _nav[_navFront]);
        BuildFireList(dt);
        Gather();
        _tally.BlitRectMulti(_tallyRect, [_state[0][_front], _state[1][_front], _state[2][_front]], [], null, 0, _tallyMaterial);
        _gpu.Mark("tally");

        int back = 1 - _front;
        _state[0][back].BlitRectMulti(
            _rect,
            [_state[0][_front], _state[1][_front], _state[2][_front], _grid],
            [_state[1][back], _state[2][back]],
            null, 0, _step);
        _gpu.Mark("step");
        _front = back;

        if (CheckGrid && RtsExperiment.Grid && ++_checkFrames == 300)
            Check();
        if (CheckHowitzers && ++_howitzerSteps % 300 == 0)
            CheckGuns();
    }

    /// <summary>
    /// Shows each simulation pass's GPU time, slowest first, beside the
    /// window's whole canvas draw: averages per frame over <see cref="ReportSeconds"/>.
    /// The canvas time covers the ground, units and scoreboard, and the lab's own panel.
    /// </summary>
    private void ReportGpu(double delta)
    {
        _drawMs += RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        _drawFrames++;
        _reportElapsed += delta;
        if (_reportElapsed < ReportSeconds)
            return;

        var passes = _gpu.Report();
        double draw = _drawMs / _drawFrames;
        _drawMs = 0;
        _drawFrames = 0;
        _reportElapsed = 0;
        if (passes.Length == 0)
            return;

        double sim = passes.Sum(p => p.Ms);
        string ranked = string.Join("  ", passes.OrderByDescending(p => p.Ms).Select(p => $"{p.Pass.ToUpperInvariant()} {p.Ms:0.00}"));
        string line = $"GPU ms/frame  {ranked}";
        string total = $"SIM {sim:0.00}   DRAW {draw:0.00} (whole window)   TOTAL {sim + draw:0.00}";
        _experiment.ShowGpu(line, total);
        if (PrintStats)
            GD.Print($"[rts-gpu] {_count} units  {line}  |  {total}");
    }

    /// <summary>A few relaxation passes of every flow field, until they have settled.</summary>
    private void Relax()
    {
        if (_navPassesRun >= NavSettlePasses)
            return;
        for (int i = 0; i < NavPasses; i++)
        {
            int back = 1 - _navFront;
            _nav[back].BlitRect(_navRect, _nav[_navFront], null, 0, _navMaterial);
            _navFront = back;
        }
        _navPassesRun += NavPasses;
    }

    /// <summary>
    /// What each faction has found, from the front state and the grid built
    /// from it. Ping-pongs, since knowledge carries over.
    /// </summary>
    private void Gather()
    {
        _intelMaterial.SetShaderParameter("use_grid", RtsExperiment.Grid);
        int back = 1 - _intelFront;
        _intel[back].BlitRectMulti(new Rect2I(0, 0, Goals, 1),
            [_intel[_intelFront], _state[0][_front], _state[1][_front], _grid], [], null, 0, _intelMaterial);
        _intelFront = back;
        _step.SetShaderParameter("intel", _intel[_intelFront]);
        _gpu.Mark("intel");
    }

    /// <summary>
    /// Every sniper firing this step, as (victim, sniper), sorted by victim with
    /// the grid's bitonic sort; the step looks its own up. A sniper's victim is
    /// past the step's scan, so its shot can't be gathered the usual way.
    /// </summary>
    private void BuildFireList(float dt)
    {
        _fireKeysMaterial.SetShaderParameter("dt", dt);
        _fireKeysMaterial.SetShaderParameter("nav_field", _nav[_navFront]);
        _fire[0].BlitRectMulti(_fireRect, [_state[0][_front], _state[1][_front]], [], null, 0, _fireKeysMaterial);
        int source = 0;
        for (int k = 2; k <= _fireSlots; k <<= 1)
        {
            for (int j = k >> 1; j > 0; j >>= 1)
            {
                _fire[1 - source].BlitRect(_fireRect, _fire[source], new Color(k, j, 0, 0), 0, _sortMaterial);
                source = 1 - source;
            }
        }
        _step.SetShaderParameter("fire_list", _fire[source]);
        _gpu.Mark("snipers");
    }

    /// <summary>
    /// Keys from the front state, sorted by cell with one blit per bitonic
    /// (k, j) pass, then copied into the grid texture with the cell table below.
    /// </summary>
    private void BuildGrid()
    {
        _keys[0].BlitRectMulti(_keysRect, [_state[0][_front], _state[1][_front]], [], null, 0, _keysMaterial);
        _gpu.Mark("keys");
        int source = 0;
        for (int k = 2; k <= _slots; k <<= 1)
        {
            for (int j = k >> 1; j > 0; j >>= 1)
            {
                // k and j ride in the modulate colour: per blit, unlike a uniform.
                _keys[1 - source].BlitRect(_keysRect, _keys[source], new Color(k, j, 0, 0), 0, _sortMaterial);
                source = 1 - source;
            }
        }
        _gpu.Mark("sort");
        _grid.BlitRect(_gridRect, _keys[source], null, 0, _cellsMaterial);
        _gpu.Mark("cells");
    }

    /// <summary>Reads the front state back and prints, per faction, howitzers alive and shells in the air.</summary>
    private void CheckGuns()
    {
        Image unit = _state[1][_front].GetImage();
        Image fx = _state[2][_front].GetImage();
        var line = new System.Text.StringBuilder($"[rts-guns] {_time:0}s");
        for (int f = 0; f < Factions; f++)
        {
            int alive = 0, flying = 0;
            for (int k = 0; k < Howitzers; k++)
            {
                int id = ((k + 1) * HowitzerEvery - 1) * Factions + f;
                if (id >= _count)
                    break;
                int x = id % Width, y = id / Width + 1;
                Color u = unit.GetPixel(x, y);
                if (u.A <= 0 && u.R > 0)
                    alive++;
                if (fx.GetPixel(x, y).A > 0)
                    flying++;
            }
            line.Append($"  f{f} {alive} up {flying} firing");
        }
        // The fire list as the step read it: the sorted half of the last build.
        int shots = 0;
        Image fire = (_fireSlots > 0 ? FireSorted() : null)?.GetImage()!;
        for (int i = 0; fire is not null && i < _fireSlots; i++)
        {
            if (fire.GetPixel(i % Width, i / Width).R < 1e8f)
                shots++;
        }
        line.Append($"  | {shots} sniper shots");

        // What each faction has found: spawners known, sectors scouted.
        Image intel = _intel[_intelFront].GetImage();
        line.Append("  | known/scouted");
        for (int f = 0; f < Factions; f++)
        {
            int known = 0, scouted = 0;
            for (int g = 0; g < Goals; g++)
            {
                if ((((int)(intel.GetPixel(g, 0).R + 0.5f) >> f) & 1) == 0)
                    continue;
                if (g < Goals - Sectors)
                    known++;
                else
                    scouted++;
            }
            line.Append($" f{f} {known}/{scouted}");
        }
        GD.Print(line.ToString());
    }

    /// <summary>The fire texture holding the sorted list after <see cref="BuildFireList"/>.</summary>
    private DrawableTexture2D FireSorted()
    {
        int passes = 0;
        for (int k = 2; k <= _fireSlots; k <<= 1)
            for (int j = k >> 1; j > 0; j >>= 1)
                passes++;
        return _fire[passes % 2];
    }

    /// <summary>Reads the grid back once and checks it against a CPU sort of the same state.</summary>
    private void Check()
    {
        // The grid was built from the state the step just read: now the back pair.
        Image motion = _state[0][1 - _front].GetImage();
        Image unit = _state[1][1 - _front].GetImage();
        Image grid = _grid.GetImage();
        int columns = Mathf.CeilToInt(_world.Size.X / Cell);
        int rows = Mathf.CeilToInt(_world.Size.Y / Cell);
        var expected = new System.Collections.Generic.List<(float Key, int Id)>();
        for (int id = 0; id < _count; id++)
        {
            Color u = unit.GetPixel(id % Width, id / Width + 1);
            if (u.A > 0 || u.R <= 0)
                continue;
            Color m = motion.GetPixel(id % Width, id / Width + 1);
            int cx = Math.Clamp((int)MathF.Floor((m.R - _world.Position.X) / Cell), 0, columns - 1);
            int cy = Math.Clamp((int)MathF.Floor((m.G - _world.Position.Y) / Cell), 0, rows - 1);
            expected.Add((cy * columns + cx, id));
        }
        expected.Sort();

        int sortedRows = _slots / Width;
        int wrongOrder = 0;
        for (int i = 0; i < _slots; i++)
        {
            Color slot = grid.GetPixel(i % Width, i / Width);
            bool match = i < expected.Count
                ? slot.R == expected[i].Key && (int)slot.G == expected[i].Id
                : slot.R >= 1e8f;
            if (!match)
                wrongOrder++;
        }
        int wrongCells = 0;
        int next = 0;
        for (int c = 0; c < _cells; c++)
        {
            int start = next;
            while (next < expected.Count && expected[next].Key < c + 1)
                next++;
            Color entry = grid.GetPixel(c % Width, sortedRows + c / Width);
            if ((int)entry.R != start || (int)entry.G != next)
                wrongCells++;
        }
        GD.Print($"[rts-check] {expected.Count} alive of {_count}, {_slots} slots, {_cells} cells: " +
                 $"{wrongOrder} slots wrong, {wrongCells} cells wrong");
    }

    /// <summary>Cleared state (every unit spawns on the first step) and fresh geometry.</summary>
    private void Allocate(int count)
    {
        _count = count;
        _rect = new Rect2I(0, 0, Width, count / Width + 1);

        // The sort needs a power of two; slots past the units are padding.
        _slots = Math.Max(Width, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)count));
        _keysRect = new Rect2I(0, 0, Width, _slots / Width);
        _gridRect = new Rect2I(0, 0, Width, _slots / Width + (_cells + Width - 1) / Width);
        for (int i = 0; i < 2; i++)
            _keys[i] = Cleared(_keysRect);
        _grid = Cleared(_gridRect);

        // One slot per sniper (one rank in twenty), padded to a power of two for the sort.
        _fireSlots = Math.Max(Width, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)(count / 20 + Factions)));
        _fireRect = new Rect2I(0, 0, Width, _fireSlots / Width);
        for (int i = 0; i < 2; i++)
            _fire[i] = Cleared(_fireRect);
        _step.SetShaderParameter("fire_slots", _fireSlots);
        _step.SetShaderParameter("sorted_rows", _slots / Width);
        _intelMaterial.SetShaderParameter("sorted_rows", _slots / Width);
        // A new battle starts knowing nothing.
        for (int i = 0; i < 2; i++)
            _intel[i] = Cleared(new Rect2I(0, 0, Goals, 1));
        _intelFront = 0;
        foreach (DrawableTexture2D[] pair in _state)
        {
            for (int i = 0; i < 2; i++)
                pair[i] = Cleared(_rect);
        }
        _front = 0;
        _hudMaterial.SetShaderParameter("per_faction", (float)count / Factions);
        _tallyRect = new Rect2I(0, 0, Width, Factions);
        _tally = Cleared(_tallyRect);
        _step.SetShaderParameter("faction_tally", _tally);

        // Rocks once, then their neighbour masks into the front nav texture; the
        // first relaxation copies both to the back, and the fields fill in over
        // the first frames. Half float: distances need nothing finer.
        if (_nav[0] is null)
        {
            for (int i = 0; i < 2; i++)
                _nav[i] = Cleared(_navRect, DrawableTexture2D.DrawableFormat.Rgbah);
            _nav[1].BlitRect(_navRect, _nav[0], null, 0, _terrainMaterial);
            _nav[0].BlitRect(_navRect, _nav[1], null, 0, _maskMaterial);
            _groundMaterial.SetShaderParameter("nav_smooth", _nav[0]);
        }
        _units.SetGeometry(UnitGeometry(count));
    }

    private static DrawableTexture2D Cleared(Rect2I rect, DrawableTexture2D.DrawableFormat format = DrawableTexture2D.DrawableFormat.Rgbaf)
    {
        var texture = new DrawableTexture2D();
        texture.Setup(rect.Size.X, rect.Size.Y, format, new Color(0, 0, 0, 0));
        return texture;
    }

    /// <summary>Twelve vertices per unit: VERTEX is (corner, part), UV the unit's texel.</summary>
    private static Geometry UnitGeometry(int count)
    {
        var geometry = new Geometry(count * UnitVertices);
        for (int id = 0; id < count; id++)
        {
            var texel = new Vector2(id % Width, id / Width + 1);
            for (int k = 0; k < UnitVertices; k++)
                geometry.Set(id * UnitVertices + k, new Vector2(k % 6, k / 6), texel);
        }
        return geometry;
    }

    /// <summary>Six vertices per scoreboard part: VERTEX is (corner, part), UV.x the faction.</summary>
    private static Geometry HudGeometry()
    {
        var geometry = new Geometry(Factions * HudParts * 6);
        for (int f = 0; f < Factions; f++)
        {
            for (int part = 0; part < HudParts; part++)
            {
                for (int k = 0; k < 6; k++)
                    geometry.Set((f * HudParts + part) * 6 + k, new Vector2(k, part), new Vector2(f, 0));
            }
        }
        return geometry;
    }

    /// <summary>One vertex per index, in order; the shaders place everything.</summary>
    private sealed class Geometry(int vertices)
    {
        public readonly int[] Indices = new int[vertices];
        public readonly Vector2[] Points = new Vector2[vertices];
        public readonly Vector2[] Uvs = new Vector2[vertices];

        public void Set(int v, Vector2 point, Vector2 uv)
        {
            Indices[v] = v;
            Points[v] = point;
            Uvs[v] = uv;
        }
    }

    /// <summary>One rect over the whole field; the ground shader paints it.</summary>
    private partial class GroundLayer : Node2D
    {
        private readonly Rect2 _world;

        public GroundLayer() { }

        public GroundLayer(ShaderMaterial material, Rect2 world)
        {
            Material = material;
            _world = world;
        }

        public override void _Draw() => DrawRect(_world, Colors.White);
    }

    /// <summary>A triangle array whose vertex shader does all the placing.</summary>
    private partial class TriangleLayer : Node2D
    {
        private static readonly Color[] White = [Colors.White];
        private readonly Rect2 _cull;
        private Geometry? _geometry;

        public TriangleLayer() { }

        public TriangleLayer(ShaderMaterial material, Rect2 cull, Geometry? geometry = null)
        {
            Material = material;
            _cull = cull;
            _geometry = geometry;
        }

        public void SetGeometry(Geometry geometry)
        {
            _geometry = geometry;
            QueueRedraw();
        }

        // The geometry sits near the origin until vertex() moves it, so its own
        // bounds would get it culled; claim the area it can reach instead.
        public override void _Ready() =>
            RenderingServer.CanvasItemSetCustomRect(GetCanvasItem(), true, _cull);

        public override void _Draw()
        {
            if (_geometry is { } g)
                RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), g.Indices, g.Points, White, g.Uvs);
        }
    }
}
