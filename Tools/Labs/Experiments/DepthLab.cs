using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Mw = MoonWorks.Graphics;
using Xanadu.Slang;
using Xanadu.Host.Tools;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Three flat quads drawn into the lab's own colour and depth-stencil target.
/// Each writes whatever depth it likes per pixel (SV_Depth: a sphere, a tilt,
/// noise…), keeps only its shape (discard, so stencil sees the shape), and has
/// the whole depth and stencil state open. Views show the depth buffer as grey
/// and the stencil buffer as colours. Drag quads on the stage.
/// </summary>
internal sealed class DepthLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;

    private static readonly string[] Shapes = ["SQUARE", "CIRCLE", "RING", "STAR", "STRIPES", "CHECKER", "BLOBS"];
    private static readonly string[] Sources = ["FLAT", "SPHERE", "TILT", "WAVES", "NOISE", "RINGS"];
    private static readonly string[] Views = ["COLOUR", "DEPTH", "STENCIL"];
    private static readonly int[][] Orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
    private static readonly string[] Names = ["A", "B", "C"];
    private static readonly Mw.CompareOp[] Compares = Enum.GetValues<Mw.CompareOp>()[1..];
    private static readonly Mw.StencilOp[] StencilOps = Enum.GetValues<Mw.StencilOp>()[1..];

    private static readonly (string Name, Action<DepthLab> Apply)[] Scenes =
    [
        ("SPHERES", lab => lab.Scene(SceneKind.Spheres)),
        ("CROSS", lab => lab.Scene(SceneKind.Cross)),
        ("OCCLUDER", lab => lab.Scene(SceneKind.Occluder)),
        ("MASK", lab => lab.Scene(SceneKind.Mask)),
        ("OUTLINE", lab => lab.Scene(SceneKind.Outline)),
        ("COUNT", lab => lab.Scene(SceneKind.Count)),
    ];

    private enum SceneKind { Spheres, Cross, Occluder, Mask, Outline, Count }

    private readonly Quad[] _quads = [new(0), new(1), new(2)];
    private readonly LabPanel _ui = new();
    private int _selected;
    private int _order;
    private int _view;
    private float _clearDepth = 1f;
    private bool _animate = true;

    private readonly Entity[] _pick = new Entity[3];
    private readonly Entity[] _scenes = new Entity[Scenes.Length];
    private Entity _on, _depthTest, _depthWrite, _colourWrite, _stencilTest, _animateToggle;
    private Entity _depth, _range, _size, _clear;
    private Entity _readout;

    public string Name => "DEPTH + STENCIL";

    public string[] Notes =>
    [
        "Drag quads on the stage. Each writes its own depth per pixel,",
        "keeps only its shape, then runs the depth and stencil tests.",
    ];

    private Quad Selected => _quads[_selected];

    public DepthLab() => Scene(SceneKind.Spheres);

    // ---- scenes -------------------------------------------------------------

    private void Scene(SceneKind kind)
    {
        for (int i = 0; i < 3; i++)
            _quads[i].Reset();
        Quad a = _quads[0], b = _quads[1], c = _quads[2];
        _view = 0;
        _order = 0;
        switch (kind)
        {
            case SceneKind.Spheres:
                a.Shape = b.Shape = c.Shape = 1;
                a.Source = b.Source = c.Source = 1;
                a.Centre = new Vector2(0.38f, 0.45f);
                b.Centre = new Vector2(0.6f, 0.45f);
                c.Centre = new Vector2(0.5f, 0.65f);
                c.Depth = 0.55f;
                break;
            case SceneKind.Cross:
                a.Source = 2;
                b.Source = 3;
                c.Source = 4;
                a.Centre = new Vector2(0.42f, 0.42f);
                b.Centre = new Vector2(0.55f, 0.5f);
                c.Centre = new Vector2(0.5f, 0.6f);
                a.Range = 0.8f;
                break;
            case SceneKind.Occluder:
                a.Shape = 3;
                a.ColourWrite = false;
                a.Depth = 0.2f;
                a.Centre = new Vector2(0.5f, 0.5f);
                a.Size = 0.45f;
                b.Source = 5;
                c.Shape = 4;
                c.Depth = 0.7f;
                b.Centre = new Vector2(0.4f, 0.45f);
                c.Centre = new Vector2(0.6f, 0.55f);
                break;
            case SceneKind.Mask:
                a.Shape = 3;
                a.ColourWrite = false;
                a.DepthTest = a.DepthWrite = false;
                a.StencilTest = true;
                a.StencilCompare = Mw.CompareOp.Always;
                a.Pass = Mw.StencilOp.Replace;
                a.Centre = new Vector2(0.5f, 0.5f);
                a.Size = 0.55f;
                b.Shape = 4;
                c.Shape = 5;
                b.DepthTest = c.DepthTest = b.DepthWrite = c.DepthWrite = false;
                b.StencilTest = c.StencilTest = true;
                b.StencilCompare = Mw.CompareOp.Equal;
                c.StencilCompare = Mw.CompareOp.NotEqual;
                b.Centre = new Vector2(0.42f, 0.5f);
                c.Centre = new Vector2(0.58f, 0.5f);
                b.Size = c.Size = 0.6f;
                break;
            case SceneKind.Outline:
                a.Shape = b.Shape = 3;
                a.ColourWrite = false;
                a.DepthTest = a.DepthWrite = b.DepthTest = b.DepthWrite = false;
                a.StencilTest = b.StencilTest = true;
                a.StencilCompare = Mw.CompareOp.Always;
                a.Pass = Mw.StencilOp.Replace;
                b.StencilCompare = Mw.CompareOp.NotEqual;
                a.Centre = b.Centre = new Vector2(0.5f, 0.5f);
                a.Size = 0.4f;
                b.Size = 0.46f;
                c.On = false;
                break;
            case SceneKind.Count:
                foreach (Quad quad in _quads)
                {
                    quad.Shape = 1;
                    quad.DepthTest = quad.DepthWrite = false;
                    quad.StencilTest = true;
                    quad.StencilCompare = Mw.CompareOp.Always;
                    quad.Pass = Mw.StencilOp.IncrementAndClamp;
                }

                a.Centre = new Vector2(0.42f, 0.42f);
                b.Centre = new Vector2(0.58f, 0.42f);
                c.Centre = new Vector2(0.5f, 0.58f);
                _view = 2;
                break;
        }
    }

    // ---- panel --------------------------------------------------------------

    public void BuildPanel(UiNode panel)
    {
        UiNode pick = LabPanel.Row(panel);
        for (int i = 0; i < 3; i++)
            _pick[i] = pick.Button("QUAD " + Names[i], UiType.Label).Size(150, RowHeight);
        _on = LabPanel.Toggle(pick, "DRAWN", "HIDDEN", true, Inner - 150 * 3 - 12, "Whether the selected quad draws at all");

        UiNode scenes = LabPanel.Row(panel);
        float width = (Inner - 4 * (Scenes.Length - 1)) / Scenes.Length;
        for (int i = 0; i < Scenes.Length; i++)
            _scenes[i] = scenes.Button(Scenes[i].Name, UiType.Caption).Size(width, RowHeight)
                .Tooltip("Set all three quads up as " + Scenes[i].Name);

        _ui.Stepper(panel, "SHAPE", () => Selected.Shape, i => Selected.Shape = i, Shapes.Length, i => Shapes[i]);
        _ui.Stepper(panel, "DEPTH FROM", () => Selected.Source, i => Selected.Source = i, Sources.Length,
            i => Sources[i] + (i == 0 ? "" : "  (SV_Depth per pixel)"));
        _depth = LabPanel.Slider(panel, "DEPTH", 0, 1, 0.5f, "Where the quad sits: 0 near, 1 far");
        _range = LabPanel.Slider(panel, "RANGE", 0, 1, 0.5f, "How far the made-up depth swings around DEPTH");
        _size = LabPanel.Slider(panel, "SIZE", 0.1f, 1, 0.4f, "The quad's side, as a share of the stage's short side");

        UiNode flags = LabPanel.Row(panel);
        _depthTest = LabPanel.Toggle(flags, "DEPTH TEST", "DEPTH TEST", true, 196, "Compare each pixel's depth with the buffer's");
        _depthWrite = LabPanel.Toggle(flags, "DEPTH WRITE", "DEPTH WRITE", true, 196,
            "Store the pixel's depth when it passes (only happens with DEPTH TEST on: use ALWAYS to write blindly)");
        _colourWrite = LabPanel.Toggle(flags, "COLOUR WRITE", "COLOUR WRITE", true, 200,
            "Off: the quad still writes depth and stencil but is invisible");

        _ui.Stepper(panel, "DEPTH PASSES", () => Array.IndexOf(Compares, Selected.DepthCompare),
            i => Selected.DepthCompare = Compares[i], Compares.Length, i => CompareName(Compares[i], "depth", "stored"));

        LabStepper reference = _ui.Stepper(panel, "STENCIL REF", () => Selected.Reference, i => Selected.Reference = i, 8,
            i => i.ToString(), spare: 220);
        _stencilTest = LabPanel.Toggle(reference.Row, "STENCIL TEST", "STENCIL TEST", false, 216,
            "Compare REF with the stencil value stored under each pixel, and update it");
        _ui.Stepper(panel, "STENCIL PASSES", () => Array.IndexOf(Compares, Selected.StencilCompare),
            i => Selected.StencilCompare = Compares[i], Compares.Length, i => CompareName(Compares[i], "ref", "stored"));
        _ui.Stepper(panel, "ON PASS", () => Array.IndexOf(StencilOps, Selected.Pass),
            i => Selected.Pass = StencilOps[i], StencilOps.Length, i => OpName(StencilOps[i]));
        _ui.Stepper(panel, "ON FAIL", () => Array.IndexOf(StencilOps, Selected.Fail),
            i => Selected.Fail = StencilOps[i], StencilOps.Length, i => OpName(StencilOps[i]));
        _ui.Stepper(panel, "ON DEPTH FAIL", () => Array.IndexOf(StencilOps, Selected.DepthFail),
            i => Selected.DepthFail = StencilOps[i], StencilOps.Length, i => OpName(StencilOps[i]));
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);

        _ui.Stepper(panel, "DRAW ORDER", () => _order, i => _order = i, Orders.Length,
            i => string.Join(" then ", Array.ConvertAll(Orders[i], q => Names[q])));
        LabStepper view = _ui.Stepper(panel, "VIEW", () => _view, i => _view = i, Views.Length, i => Views[i], spare: 150);
        _animateToggle = LabPanel.Toggle(view.Row, "ANIMATE", "ANIMATE", true, 146, "Move the waves, noise, rings and blobs");
        _clear = LabPanel.Slider(panel, "CLEAR DEPTH", 0, 1, 1, "What the depth buffer starts each frame at");

        ShowQuad();
    }

    private static string CompareName(Mw.CompareOp op, string left, string right) => op switch
    {
        Mw.CompareOp.Never => "NEVER",
        Mw.CompareOp.Less => $"LESS  ({left} < {right})",
        Mw.CompareOp.Equal => $"EQUAL  ({left} = {right})",
        Mw.CompareOp.LessOrEqual => $"LESS OR EQUAL  ({left} ≤ {right})",
        Mw.CompareOp.Greater => $"GREATER  ({left} > {right})",
        Mw.CompareOp.NotEqual => $"NOT EQUAL  ({left} ≠ {right})",
        Mw.CompareOp.GreaterOrEqual => $"GREATER OR EQUAL  ({left} ≥ {right})",
        _ => "ALWAYS",
    };

    private static string OpName(Mw.StencilOp op) => op switch
    {
        Mw.StencilOp.Keep => "KEEP",
        Mw.StencilOp.Zero => "ZERO",
        Mw.StencilOp.Replace => "REPLACE  (store REF)",
        Mw.StencilOp.IncrementAndClamp => "INCREMENT  (stops at 255)",
        Mw.StencilOp.DecrementAndClamp => "DECREMENT  (stops at 0)",
        Mw.StencilOp.Invert => "INVERT  (flip every bit)",
        Mw.StencilOp.IncrementAndWrap => "INCREMENT WRAP  (255 → 0)",
        _ => "DECREMENT WRAP  (0 → 255)",
    };

    private static string Short(Mw.StencilOp op) => op switch
    {
        Mw.StencilOp.Keep => "keep",
        Mw.StencilOp.Zero => "0",
        Mw.StencilOp.Replace => "ref",
        Mw.StencilOp.IncrementAndClamp or Mw.StencilOp.IncrementAndWrap => "+1",
        Mw.StencilOp.DecrementAndClamp or Mw.StencilOp.DecrementAndWrap => "-1",
        _ => "~",
    };

    /// <summary>Reads the panel into the selected quad and the shared settings, pushing values back when a button rewrites them.</summary>
    private void Poll()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_ui.Clicked(_pick[i]))
                Select(i);
        }

        for (int i = 0; i < Scenes.Length; i++)
        {
            if (_ui.Clicked(_scenes[i]))
            {
                Scenes[i].Apply(this);
                ShowQuad();
            }
        }

        _ui.PollSteppers();

        Quad quad = Selected;
        quad.On = LabPanel.On(_on);
        quad.DepthTest = LabPanel.On(_depthTest);
        quad.DepthWrite = LabPanel.On(_depthWrite);
        quad.ColourWrite = LabPanel.On(_colourWrite);
        quad.StencilTest = LabPanel.On(_stencilTest);
        quad.Depth = LabPanel.Value(_depth);
        quad.Range = LabPanel.Value(_range);
        quad.Size = LabPanel.Value(_size);
        _clearDepth = LabPanel.Value(_clear);
        _animate = LabPanel.On(_animateToggle);

        _ui.ShowSteppers();
        UiText.Set(_readout, quad.StencilTest
            ? $"stencil: {quad.Reference} {Symbol(quad.StencilCompare)} stored ? pass: {Short(quad.Pass)}, fail: {Short(quad.Fail)}, depth fail: {Short(quad.DepthFail)}"
            : "stencil: off (passes, writes nothing)");
    }

    private static string Symbol(Mw.CompareOp op) => op switch
    {
        Mw.CompareOp.Never => "never",
        Mw.CompareOp.Less => "<",
        Mw.CompareOp.Equal => "=",
        Mw.CompareOp.LessOrEqual => "<=",
        Mw.CompareOp.Greater => ">",
        Mw.CompareOp.NotEqual => "!=",
        Mw.CompareOp.GreaterOrEqual => ">=",
        _ => "always",
    };

    private void Select(int index)
    {
        _selected = index;
        ShowQuad();
    }

    /// <summary>Puts the selected quad's values on the panel's controls.</summary>
    private void ShowQuad()
    {
        if (_on.IsNull)
            return;

        Quad quad = Selected;
        LabPanel.ShowChoice(_pick, _selected);
        LabPanel.SetOn(_on, quad.On);
        LabPanel.SetOn(_depthTest, quad.DepthTest);
        LabPanel.SetOn(_depthWrite, quad.DepthWrite);
        LabPanel.SetOn(_colourWrite, quad.ColourWrite);
        LabPanel.SetOn(_stencilTest, quad.StencilTest);
        LabPanel.SetValue(_depth, quad.Depth);
        LabPanel.SetValue(_range, quad.Range);
        LabPanel.SetValue(_size, quad.Size);
    }

    private sealed class Quad(int index)
    {
        private static readonly Color[] Colours = [new(1f, 0.55f, 0.15f), new(0.2f, 0.65f, 1f), new(0.55f, 0.95f, 0.3f)];

        public readonly Color Colour = Colours[index];
        public bool On;
        public int Shape, Source;
        public float Depth, Range, Size;
        public Vector2 Centre;
        public bool DepthTest, DepthWrite, ColourWrite, StencilTest;
        public Mw.CompareOp DepthCompare, StencilCompare;
        public Mw.StencilOp Pass, Fail, DepthFail;
        public int Reference;

        public void Reset()
        {
            On = true;
            Shape = Source = 0;
            Depth = 0.5f;
            Range = 0.5f;
            Size = 0.4f;
            Centre = new Vector2(0.5f, 0.5f);
            DepthTest = DepthWrite = ColourWrite = true;
            StencilTest = false;
            DepthCompare = Mw.CompareOp.Less;
            StencilCompare = Mw.CompareOp.Always;
            Pass = Fail = DepthFail = Mw.StencilOp.Keep;
            Reference = 1;
        }

        /// <summary>Its box in stage pixels (the stage's top left is 0, 0).</summary>
        public Rect2 Box(Vector2 stage)
        {
            float side = MathF.Min(stage.X, stage.Y) * Size;
            Vector2 centre = Centre * stage;
            return new Rect2(centre - new Vector2(side, side) / 2, new Vector2(side, side));
        }

        public State Pipeline => new(DepthTest, DepthWrite, DepthCompare, StencilTest, StencilCompare, Pass, Fail, DepthFail, ColourWrite);
    }

    /// <summary>Everything about a quad that lives in its pipeline; the stencil reference and uniforms are set per draw.</summary>
    private readonly record struct State(
        bool DepthTest,
        bool DepthWrite,
        Mw.CompareOp DepthCompare,
        bool StencilTest,
        Mw.CompareOp StencilCompare,
        Mw.StencilOp Pass,
        Mw.StencilOp Fail,
        Mw.StencilOp DepthFail,
        bool ColourWrite);

    // ---- stage --------------------------------------------------------------

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct QuadUniforms
    {
        public float X, Y, Width, Height;
        public float R, G, B, A;
        public float Source, Depth, Range, Time;
        public float Shape, Pad, TargetWidth, TargetHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlatUniforms
    {
        public float R, G, B, A;
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat ColourFormat = Mw.TextureFormat.R8G8B8A8Unorm;

        /// <summary>Stencil values 0 to 7 in the stencil view; anything above shows white.</summary>
        private static readonly Color[] StencilColours =
        [
            new(0.08f, 0.08f, 0.1f), new(0.9f, 0.2f, 0.2f), new(1f, 0.55f, 0.1f), new(1f, 0.9f, 0.2f),
            new(0.3f, 0.85f, 0.3f), new(0.2f, 0.85f, 0.9f), new(0.25f, 0.4f, 1f), new(0.85f, 0.3f, 0.95f),
        ];

        private readonly DepthLab _lab;
        private readonly LabContext _context;
        private readonly Rect2 _area;
        private readonly uint _width, _height;
        private readonly Mw.GraphicsDevice _device;
        private readonly Mw.Texture _colour, _depth;
        private readonly Mw.TextureFormat _depthFormat;
        private readonly bool _depthReadable;
        private readonly Mw.Sampler _sampler;
        private readonly Mw.Shader _quadVertex, _quadFragment, _fullVertex, _showDepth, _flat;
        private readonly Dictionary<State, Mw.GraphicsPipeline> _pipelines = new();
        private readonly Mw.GraphicsPipeline _stencilEqual, _stencilAbove;
        private readonly Mw.GraphicsPipeline? _depthView;
        private readonly Handle _item, _shown;
        private float _time;
        private int _dragging = -1;
        private Vector2 _grab;
        private bool _wasHeld;

        public Stage(DepthLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _area = area;
            _device = context.Device;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            // Sampling the depth view needs a depth-stencil format the GPU can also read.
            const Mw.TextureUsageFlags target = Mw.TextureUsageFlags.DepthStencilTarget;
            const Mw.TextureUsageFlags readable = target | Mw.TextureUsageFlags.Sampler;
            _depthFormat = Supports(Mw.TextureFormat.D24UnormS8Uint, readable) ? Mw.TextureFormat.D24UnormS8Uint
                : Supports(Mw.TextureFormat.D32FloatS8Uint, readable) ? Mw.TextureFormat.D32FloatS8Uint
                : _device.SupportedDepthStencilFormat;
            _depthReadable = Supports(_depthFormat, readable);

            _colour = Mw.Texture.Create2D(_device, "depth lab colour", _width, _height, ColourFormat,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _depth = Mw.Texture.Create2D(_device, "depth lab depth", _width, _height, _depthFormat,
                _depthReadable ? readable : target);
            _sampler = Mw.Sampler.Create(_device, Mw.SamplerCreateInfo.PointClamp);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "depth_lab.slang");
            _quadVertex = Shader(module, "quadMain", "vertex", Mw.ShaderStage.Vertex, uniforms: 1);
            _quadFragment = Shader(module, "quadShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);
            _fullVertex = Shader(module, "full", "vertex", Mw.ShaderStage.Vertex);
            _showDepth = Shader(module, "showDepth", "fragment", Mw.ShaderStage.Fragment, samplers: 1);
            _flat = Shader(module, "flatColour", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);

            _stencilEqual = StencilView(Mw.CompareOp.Equal);
            _stencilAbove = StencilView(Mw.CompareOp.Less);
            if (_depthReadable)
                _depthView = Create(_fullVertex, _showDepth, Mw.DepthStencilState.Disable, Opaque, depth: false);

            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            _shown = context.Show(_colour);
            CanvasServer.Current.AddTextureRect(_item, area, _shown);
        }

        private bool Supports(Mw.TextureFormat format, Mw.TextureUsageFlags usage) =>
            _device.SupportsTextureFormat(format, Mw.TextureType.TwoDimensional, usage);

        private Mw.Shader Shader(string module, string entry, string stage, Mw.ShaderStage kind, uint uniforms = 0, uint samplers = 0) =>
            Mw.Shader.Create(_device, SlangShaders.Compile(module, entry, stage), "main", new Mw.ShaderCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                Stage = kind,
                NumUniformBuffers = uniforms,
                NumSamplers = samplers,
            });

        private static Mw.ColorTargetBlendState Opaque => CanvasPipelines.Blend(Xanadu.Rendering.BlendMode.Mix);

        private Mw.GraphicsPipeline Create(Mw.Shader vertex, Mw.Shader fragment, Mw.DepthStencilState state,
            Mw.ColorTargetBlendState blend, bool depth = true) =>
            Mw.GraphicsPipeline.Create(_device, new Mw.GraphicsPipelineCreateInfo
            {
                VertexShader = vertex,
                FragmentShader = fragment,
                VertexInputState = Mw.VertexInputState.Empty,
                PrimitiveType = Mw.PrimitiveType.TriangleList,
                RasterizerState = Mw.RasterizerState.CCW_CullNone,
                MultisampleState = Mw.MultisampleState.None,
                DepthStencilState = state,
                TargetInfo = new Mw.GraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = ColourFormat, BlendState = blend }],
                    HasDepthStencilTarget = depth,
                    DepthStencilFormat = depth ? _depthFormat : default,
                },
            });

        /// <summary>A full-screen pass that paints where the stencil value compares with its reference.</summary>
        private Mw.GraphicsPipeline StencilView(Mw.CompareOp compare)
        {
            var keep = new Mw.StencilOpState
            {
                CompareOp = compare,
                PassOp = Mw.StencilOp.Keep,
                FailOp = Mw.StencilOp.Keep,
                DepthFailOp = Mw.StencilOp.Keep,
            };
            return Create(_fullVertex, _flat, new Mw.DepthStencilState
            {
                EnableStencilTest = true,
                FrontStencilState = keep,
                BackStencilState = keep,
                CompareMask = 0xFF,
                WriteMask = 0,
            }, Opaque);
        }

        private Mw.GraphicsPipeline Pipeline(State state)
        {
            if (_pipelines.TryGetValue(state, out Mw.GraphicsPipeline? pipeline))
                return pipeline;

            var stencil = new Mw.StencilOpState
            {
                CompareOp = state.StencilCompare,
                PassOp = state.Pass,
                FailOp = state.Fail,
                DepthFailOp = state.DepthFail,
            };
            pipeline = Create(_quadVertex, _quadFragment, new Mw.DepthStencilState
            {
                EnableDepthTest = state.DepthTest,
                EnableDepthWrite = state.DepthWrite,
                CompareOp = state.DepthCompare,
                EnableStencilTest = state.StencilTest,
                FrontStencilState = stencil,
                BackStencilState = stencil,
                CompareMask = 0xFF,
                WriteMask = 0xFF,
            }, state.ColourWrite ? Opaque : Mw.ColorTargetBlendState.NoWrite);
            _pipelines[state] = pipeline;
            return pipeline;
        }

        public void Update(in ToolFrame frame)
        {
            _lab.Poll();
            if (_lab._animate)
                _time += frame.Delta;
            Drag(frame);
        }

        /// <summary>A press on the stage picks the topmost drawn quad under it; holding drags it.</summary>
        private void Drag(in ToolFrame frame)
        {
            Vector2 local = frame.Mouse - _area.Position;
            bool started = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;
            if (!frame.Held)
            {
                _dragging = -1;
                return;
            }

            if (started && _area.HasPoint(frame.Mouse))
            {
                int[] order = Orders[_lab._order];
                for (int i = order.Length - 1; i >= 0; i--)
                {
                    Quad quad = _lab._quads[order[i]];
                    if (!quad.On || !quad.Box(_area.Size).HasPoint(local))
                        continue;
                    _dragging = order[i];
                    _grab = local - quad.Centre * _area.Size;
                    _lab.Select(_dragging);
                    break;
                }
            }

            if (_dragging >= 0)
            {
                Vector2 centre = (local - _grab) / _area.Size;
                _lab._quads[_dragging].Centre = new Vector2(Mathf.Clamp(centre.X, 0, 1), Mathf.Clamp(centre.Y, 0, 1));
            }
        }

        public void Render(Mw.CommandBuffer commands)
        {
            var depthTarget = new Mw.DepthStencilTargetInfo(_depth, _lab._clearDepth, 0, cycle: true)
            {
                StoreOp = Mw.StoreOp.Store,
                StencilStoreOp = Mw.StoreOp.Store,
            };
            Mw.RenderPass pass = commands.BeginRenderPass(depthTarget,
                new Mw.ColorTargetInfo(_colour, new Mw.Color(0.06f, 0.07f, 0.09f, 1f), cycle: true));
            var stage = new Vector2(_width, _height);
            foreach (int index in Orders[_lab._order])
            {
                Quad quad = _lab._quads[index];
                if (!quad.On)
                    continue;

                Rect2 box = quad.Box(stage);
                var uniforms = new QuadUniforms
                {
                    X = box.Position.X, Y = box.Position.Y, Width = box.Size.X, Height = box.Size.Y,
                    R = quad.Colour.R, G = quad.Colour.G, B = quad.Colour.B, A = 1,
                    Source = quad.Source, Depth = quad.Depth, Range = quad.Range, Time = _time,
                    Shape = quad.Shape, TargetWidth = _width, TargetHeight = _height,
                };
                pass.BindGraphicsPipeline(Pipeline(quad.Pipeline));
                pass.SetStencilReference((byte)quad.Reference);
                commands.PushVertexUniformData(uniforms);
                commands.PushFragmentUniformData(uniforms);
                pass.DrawPrimitives(6, 1, 0, 0);
            }

            if (_lab._view == 2)
            {
                // Each stencil value paints its own colour: a draw per value, passing only where stored = ref.
                pass.BindGraphicsPipeline(_stencilEqual);
                for (int value = 0; value < StencilColours.Length; value++)
                {
                    Color colour = StencilColours[value];
                    pass.SetStencilReference((byte)value);
                    commands.PushFragmentUniformData(new FlatUniforms { R = colour.R, G = colour.G, B = colour.B, A = 1 });
                    pass.DrawPrimitives(3, 1, 0, 0);
                }

                // Passes where 7 < stored: everything above the palette.
                pass.BindGraphicsPipeline(_stencilAbove);
                pass.SetStencilReference((byte)(StencilColours.Length - 1));
                commands.PushFragmentUniformData(new FlatUniforms { R = 1, G = 1, B = 1, A = 1 });
                pass.DrawPrimitives(3, 1, 0, 0);
            }

            commands.EndRenderPass(pass);

            if (_lab._view == 1 && _depthView is not null)
            {
                Mw.RenderPass view = commands.BeginRenderPass(new Mw.ColorTargetInfo(_colour, Mw.LoadOp.DontCare));
                view.BindGraphicsPipeline(_depthView);
                view.BindFragmentSamplers(new Mw.TextureSamplerBinding(_depth, _sampler));
                view.DrawPrimitives(3, 1, 0, 0);
                commands.EndRenderPass(view);
            }
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (Mw.GraphicsPipeline pipeline in _pipelines.Values)
                pipeline.Dispose();
            _stencilEqual.Dispose();
            _stencilAbove.Dispose();
            _depthView?.Dispose();
            foreach (Mw.Shader shader in (Mw.Shader[])[_quadVertex, _quadFragment, _fullVertex, _showDepth, _flat])
                shader.Dispose();
            _sampler.Dispose();
            _colour.Dispose();
            _depth.Dispose();
        }
    }
}
