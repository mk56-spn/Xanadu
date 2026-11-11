using System;
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
/// Order-dependent against order-independent transparency: one set of
/// translucent layers drawn four ways side by side. Plain "over" blending in
/// draw order, the same sorted back to front by each layer's centre, weighted
/// blended OIT (one pass, no sorting, an approximation), and an exact per-pixel
/// sort as the reference. Tilted layers cross each other, which no per-layer
/// sort can get right.
/// </summary>
internal sealed class TransparencyLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;
    private const int MaxLayers = 32;

    private static readonly string[] Panes = ["DRAW ORDER", "SORTED BY CENTRE", "WEIGHTED OIT", "EXACT (PER PIXEL)"];

    private static readonly (string Name, Action<TransparencyLab> Apply)[] Scenes =
    [
        ("STACK", lab => lab.Scene(SceneKind.Stack)),
        ("CROSSING", lab => lab.Scene(SceneKind.Crossing)),
        ("SMOKE", lab => lab.Scene(SceneKind.Smoke)),
        ("GLASS", lab => lab.Scene(SceneKind.Glass)),
    ];

    private enum SceneKind { Stack, Crossing, Smoke, Glass }

    private Layer[] _layers = [];
    private readonly LabPanel _ui = new();
    private float _swing, _alpha = 1, _weight = 4;
    private bool _animate = true;

    private readonly Entity[] _scenes = new Entity[Scenes.Length];
    private Entity _swingSlider, _alphaSlider, _weightSlider, _animateToggle, _readout;

    public string Name => "TRANSPARENCY";

    public string[] Notes =>
    [
        "The same translucent layers, four ways. Top: plain blending",
        "(order dependent). Bottom: OIT and the exact per-pixel answer.",
    ];

    public TransparencyLab() => Scene(SceneKind.Stack);

    // ---- scenes -------------------------------------------------------------

    private void Scene(SceneKind kind)
    {
        Color red = new(0.95f, 0.2f, 0.2f), green = new(0.25f, 0.9f, 0.3f), blue = new(0.2f, 0.45f, 1f),
            yellow = new(1f, 0.85f, 0.15f);
        _alpha = 1;
        switch (kind)
        {
            case SceneKind.Stack:
                // Drawn in index order, but the depths say otherwise; the swing keeps reshuffling them.
                _layers =
                [
                    new(new(0.42f, 0.4f), 0.42f, red, 0.6f, 0.3f, phase: 0),
                    new(new(0.54f, 0.48f), 0.42f, green, 0.6f, 0.6f, phase: 1.6f),
                    new(new(0.62f, 0.62f), 0.42f, blue, 0.6f, 0.45f, phase: 3.1f),
                    new(new(0.48f, 0.66f), 0.38f, yellow, 0.6f, 0.75f, phase: 4.7f),
                ];
                _swing = 0.25f;
                break;
            case SceneKind.Crossing:
                // Tilted layers pierce each other: each is in front on one side and behind on the other.
                _layers =
                [
                    new(new(0.45f, 0.45f), 0.55f, red, 0.6f, 0.5f, tilt: 0.9f),
                    new(new(0.55f, 0.5f), 0.55f, blue, 0.6f, 0.5f, tilt: -0.9f),
                    new(new(0.5f, 0.58f), 0.5f, green, 0.6f, 0.5f, shape: 1, phase: 2),
                ];
                _swing = 0.1f;
                break;
            case SceneKind.Smoke:
                // Many soft, similar puffs: where weighted OIT is close to exact.
                var random = new Random(7);
                _layers = new Layer[24];
                for (int i = 0; i < _layers.Length; i++)
                {
                    float warm = (float)random.NextDouble();
                    var colour = new Color(0.55f + 0.45f * warm, 0.45f + 0.2f * warm, 0.4f - 0.2f * warm);
                    _layers[i] = new(
                        new(0.25f + 0.5f * (float)random.NextDouble(), 0.25f + 0.5f * (float)random.NextDouble()),
                        0.25f + 0.25f * (float)random.NextDouble(), colour, 0.55f, (float)random.NextDouble(),
                        shape: 2, phase: (float)random.NextDouble() * MathF.Tau, drift: 0.05f);
                }

                _swing = 0.3f;
                break;
            case SceneKind.Glass:
                // Few strong, very different layers: where weighted OIT can't tell which one is in front.
                _layers =
                [
                    new(new(0.42f, 0.45f), 0.45f, red, 0.9f, 0.3f, phase: 0),
                    new(new(0.58f, 0.5f), 0.45f, blue, 0.9f, 0.6f, phase: 2.1f),
                    new(new(0.5f, 0.6f), 0.4f, green, 0.9f, 0.45f, shape: 1, phase: 4.2f),
                ];
                _swing = 0.3f;
                break;
        }

        if (!_swingSlider.IsNull)
        {
            LabPanel.SetValue(_swingSlider, _swing);
            LabPanel.SetValue(_alphaSlider, _alpha);
        }
    }

    // ---- panel --------------------------------------------------------------

    public void BuildPanel(UiNode panel)
    {
        UiNode scenes = LabPanel.Row(panel);
        float width = (Inner - 4 * (Scenes.Length - 1)) / Scenes.Length;
        for (int i = 0; i < Scenes.Length; i++)
            _scenes[i] = scenes.Button(Scenes[i].Name, UiType.Caption).Size(width, RowHeight)
                .Tooltip("Load the " + Scenes[i].Name + " layers");

        // Where each way of drawing sits on the stage.
        for (int row = 0; row < 2; row++)
        {
            UiNode legend = LabPanel.Row(panel);
            for (int column = 0; column < 2; column++)
                legend.Text(Panes[row * 2 + column], UiType.Caption, Palette.Text).Size((Inner - 4) / 2, RowHeight);
        }

        _alphaSlider = LabPanel.Slider(panel, "ALPHA ×", 0, 1.5f, 1, "Scales every layer's opacity (capped at fully opaque)");
        _swingSlider = LabPanel.Slider(panel, "DEPTH SWING", 0, 0.5f, 0.25f, "How far each layer's depth moves back and forth, reshuffling the order");
        _weightSlider = LabPanel.Slider(panel, "OIT WEIGHT", 0, 12, 4,
            "Weighted OIT only: how much nearer layers outweigh farther ones (0 = a plain average, blind to depth)", "0.0");
        UiNode flags = LabPanel.Row(panel);
        _animateToggle = LabPanel.Toggle(flags, "ANIMATE", "ANIMATE", true, 196, "Move the layers' depths (and the smoke)");
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);

        panel.Text("DRAW ORDER: blends each layer over what's there; wrong", UiType.Small, Palette.TextMuted);
        panel.Text("whenever a later layer is behind an earlier one.", UiType.Small, Palette.TextMuted);
        panel.Text("SORTED: right until layers cross; pops as the order flips.", UiType.Small, Palette.TextMuted);
        panel.Text("WEIGHTED OIT: one pass, any order, no popping, but only", UiType.Small, Palette.TextMuted);
        panel.Text("an average: strong layers smear into each other.", UiType.Small, Palette.TextMuted);
        panel.Text("EXACT: sorts every layer under each pixel (the reference;", UiType.Small, Palette.TextMuted);
        panel.Text("fine for 32 quads here, not for a real scene).", UiType.Small, Palette.TextMuted);

        Scene(SceneKind.Stack);
    }

    private void Poll()
    {
        for (int i = 0; i < Scenes.Length; i++)
        {
            if (_ui.Clicked(_scenes[i]))
                Scenes[i].Apply(this);
        }

        _swing = LabPanel.Value(_swingSlider);
        _alpha = LabPanel.Value(_alphaSlider);
        _weight = LabPanel.Value(_weightSlider);
        _animate = LabPanel.On(_animateToggle);
    }

    private sealed class Layer(Vector2 centre, float size, Color colour, float alpha, float depth,
        float tilt = 0, int shape = 0, float phase = 0, float drift = 0)
    {
        public readonly Vector2 Centre = centre;
        public readonly float Size = size, Alpha = alpha, Depth = depth, Tilt = tilt, Phase = phase, Drift = drift;
        public readonly Color Colour = colour;
        public readonly int Shape = shape;
    }

    // ---- stage --------------------------------------------------------------

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct LayerData
    {
        public float X, Y, Width, Height;
        public float R, G, B, A;
        public float Depth, Tilt, Shape, Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public LayerData Layer;
        public float TargetWidth, TargetHeight, Weight, Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct SceneUniforms
    {
        public float TargetWidth, TargetHeight, Count, Pad;
        public fixed float Layers[MaxLayers * 12];
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat ColourFormat = Mw.TextureFormat.R8G8B8A8Unorm;
        private const Mw.TextureFormat AccumFormat = Mw.TextureFormat.R16G16B16A16Float;
        private const Mw.TextureFormat RevealFormat = Mw.TextureFormat.R16Float;
        private const float Gap = 8;

        private readonly TransparencyLab _lab;
        private readonly LabContext _context;
        private readonly uint _width, _height;
        private readonly Mw.GraphicsDevice _device;
        private readonly Mw.Texture[] _panes = new Mw.Texture[4];
        private readonly Handle[] _shown = new Handle[4];
        private readonly Mw.Texture _accum, _reveal;
        private readonly Mw.Sampler _sampler;
        private readonly Mw.Shader _layerVertex, _blendShade, _oitShade, _fullVertex, _composite, _exact, _back;
        private readonly Mw.GraphicsPipeline _blend, _oit, _compositePipe, _exactPipe, _backPipe;
        private readonly Handle _item;
        private readonly LayerData[] _frame = new LayerData[MaxLayers];
        private readonly int[] _sorted = new int[MaxLayers];
        private readonly Comparer _farFirst;
        private float _time;

        public Stage(TransparencyLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _device = context.Device;
            _farFirst = new Comparer(_frame);
            var pane = new Vector2((area.Size.X - Gap) / 2, (area.Size.Y - Gap) / 2);
            _width = (uint)pane.X;
            _height = (uint)pane.Y;

            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            for (int i = 0; i < 4; i++)
            {
                _panes[i] = Mw.Texture.Create2D(_device, "transparency lab " + Panes[i], _width, _height, ColourFormat,
                    Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
                _shown[i] = context.Show(_panes[i]);
                var at = area.Position + new Vector2(i % 2, i / 2) * (pane + new Vector2(Gap, Gap));
                CanvasServer.Current.AddTextureRect(_item, new Rect2(at, new Vector2(_width, _height)), _shown[i]);
            }

            const Mw.TextureUsageFlags target = Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler;
            _accum = Mw.Texture.Create2D(_device, "transparency lab accum", _width, _height, AccumFormat, target);
            _reveal = Mw.Texture.Create2D(_device, "transparency lab reveal", _width, _height, RevealFormat, target);
            _sampler = Mw.Sampler.Create(_device, Mw.SamplerCreateInfo.PointClamp);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "transparency_lab.slang");
            _layerVertex = Shader(module, "layerVertex", "vertex", Mw.ShaderStage.Vertex, uniforms: 1);
            _blendShade = Shader(module, "blendShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);
            _oitShade = Shader(module, "oitShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);
            _fullVertex = Shader(module, "full", "vertex", Mw.ShaderStage.Vertex);
            _composite = Shader(module, "oitComposite", "fragment", Mw.ShaderStage.Fragment, samplers: 2);
            _exact = Shader(module, "exactShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);
            _back = Shader(module, "back", "fragment", Mw.ShaderStage.Fragment);

            Mw.ColorTargetBlendState premultiplied = Blend(Mw.BlendFactor.One, Mw.BlendFactor.OneMinusSrcAlpha);
            _blend = Create(_layerVertex, _blendShade, new Mw.ColorTargetDescription { Format = ColourFormat, BlendState = premultiplied });
            _oit = Create(_layerVertex, _oitShade,
                new Mw.ColorTargetDescription { Format = AccumFormat, BlendState = Blend(Mw.BlendFactor.One, Mw.BlendFactor.One) },
                new Mw.ColorTargetDescription
                {
                    Format = RevealFormat, BlendState = Blend(Mw.BlendFactor.Zero, Mw.BlendFactor.OneMinusSrcColor),
                });
            _compositePipe = Create(_fullVertex, _composite, new Mw.ColorTargetDescription
            {
                Format = ColourFormat, BlendState = Blend(Mw.BlendFactor.SrcAlpha, Mw.BlendFactor.OneMinusSrcAlpha),
            });
            _exactPipe = Create(_fullVertex, _exact, new Mw.ColorTargetDescription { Format = ColourFormat, BlendState = premultiplied });
            _backPipe = Create(_fullVertex, _back, new Mw.ColorTargetDescription
            {
                Format = ColourFormat, BlendState = Mw.ColorTargetBlendState.NoBlend,
            });
        }

        private static Mw.ColorTargetBlendState Blend(Mw.BlendFactor source, Mw.BlendFactor destination) => new()
        {
            EnableBlend = true,
            SrcColorBlendFactor = source,
            DstColorBlendFactor = destination,
            ColorBlendOp = Mw.BlendOp.Add,
            SrcAlphaBlendFactor = source,
            DstAlphaBlendFactor = destination,
            AlphaBlendOp = Mw.BlendOp.Add,
            EnableColorWriteMask = true,
            ColorWriteMask = Mw.ColorComponentFlags.RGBA,
        };

        private Mw.Shader Shader(string module, string entry, string stage, Mw.ShaderStage kind, uint uniforms = 0, uint samplers = 0) =>
            Mw.Shader.Create(_device, SlangShaders.Compile(module, entry, stage), "main", new Mw.ShaderCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                Stage = kind,
                NumUniformBuffers = uniforms,
                NumSamplers = samplers,
            });

        private Mw.GraphicsPipeline Create(Mw.Shader vertex, Mw.Shader fragment, params Mw.ColorTargetDescription[] targets) =>
            Mw.GraphicsPipeline.Create(_device, new Mw.GraphicsPipelineCreateInfo
            {
                VertexShader = vertex,
                FragmentShader = fragment,
                VertexInputState = Mw.VertexInputState.Empty,
                PrimitiveType = Mw.PrimitiveType.TriangleList,
                RasterizerState = Mw.RasterizerState.CCW_CullNone,
                MultisampleState = Mw.MultisampleState.None,
                DepthStencilState = Mw.DepthStencilState.Disable,
                TargetInfo = new Mw.GraphicsPipelineTargetInfo { ColorTargetDescriptions = targets },
            });

        public void Update(in ToolFrame frame)
        {
            _lab.Poll();
            if (_lab._animate)
                _time += frame.Delta;
        }

        /// <summary>Where every layer is this frame, in pane pixels, and the back-to-front order of their centres.</summary>
        private int Layout()
        {
            Layer[] layers = _lab._layers;
            int count = Math.Min(layers.Length, MaxLayers);
            float side = MathF.Min(_width, _height);
            var pane = new Vector2(_width, _height);
            for (int i = 0; i < count; i++)
            {
                Layer layer = layers[i];
                float angle = _time * 0.8f + layer.Phase;
                Vector2 centre = (layer.Centre + layer.Drift * new Vector2(MathF.Sin(angle * 0.7f), MathF.Cos(angle * 0.5f))) * pane;
                float size = layer.Size * side;
                _frame[i] = new LayerData
                {
                    X = centre.X - size / 2, Y = centre.Y - size / 2, Width = size, Height = size,
                    R = layer.Colour.R, G = layer.Colour.G, B = layer.Colour.B,
                    A = Math.Clamp(layer.Alpha * _lab._alpha, 0, 1),
                    Depth = Math.Clamp(layer.Depth + _lab._swing * MathF.Sin(angle), 0, 1),
                    Tilt = layer.Tilt, Shape = layer.Shape,
                };
                _sorted[i] = i;
            }

            // Back to front: farthest centre first; ties keep draw order.
            Array.Sort(_sorted, 0, count, _farFirst);
            UiText.Set(_lab._readout,
                "draw order: " + Names(count, i => i) + "   ·   sorted (back to front): " + Names(count, i => _sorted[i]));
            return count;
        }

        private static string Names(int count, Func<int, int> at)
        {
            if (count > 8)
                return $"{count} layers";
            var names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = ((char)('A' + at(i))).ToString();
            return string.Join(" ", names);
        }

        private sealed class Comparer(LayerData[] frame) : System.Collections.Generic.IComparer<int>
        {
            public int Compare(int a, int b)
            {
                int byDepth = frame[b].Depth.CompareTo(frame[a].Depth);
                return byDepth != 0 ? byDepth : a.CompareTo(b);
            }
        }

        public unsafe void Render(Mw.CommandBuffer commands)
        {
            int count = Layout();

            // Weighted OIT: every layer, in any order, into the sum and the product.
            Mw.RenderPass oit = commands.BeginRenderPass(
                new Mw.ColorTargetInfo(_accum, new Mw.Color(0f, 0f, 0f, 0f), cycle: true),
                new Mw.ColorTargetInfo(_reveal, new Mw.Color(1f, 1f, 1f, 1f), cycle: true));
            oit.BindGraphicsPipeline(_oit);
            for (int i = 0; i < count; i++)
                DrawLayer(commands, oit, i);
            commands.EndRenderPass(oit);

            for (int p = 0; p < 4; p++)
            {
                Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_panes[p], Mw.LoadOp.DontCare));
                pass.BindGraphicsPipeline(_backPipe);
                pass.DrawPrimitives(3, 1, 0, 0);
                switch (p)
                {
                    case 0:
                    case 1:
                        pass.BindGraphicsPipeline(_blend);
                        for (int i = 0; i < count; i++)
                            DrawLayer(commands, pass, p == 0 ? i : _sorted[i]);
                        break;
                    case 2:
                        pass.BindGraphicsPipeline(_compositePipe);
                        pass.BindFragmentSamplers(new Mw.TextureSamplerBinding(_accum, _sampler),
                            new Mw.TextureSamplerBinding(_reveal, _sampler));
                        pass.DrawPrimitives(3, 1, 0, 0);
                        break;
                    default:
                        var scene = new SceneUniforms { TargetWidth = _width, TargetHeight = _height, Count = count };
                        fixed (LayerData* source = _frame)
                            Buffer.MemoryCopy(source, scene.Layers, MaxLayers * 12 * sizeof(float), count * sizeof(LayerData));
                        pass.BindGraphicsPipeline(_exactPipe);
                        commands.PushFragmentUniformData(scene);
                        pass.DrawPrimitives(3, 1, 0, 0);
                        break;
                }

                commands.EndRenderPass(pass);
            }
        }

        private void DrawLayer(Mw.CommandBuffer commands, Mw.RenderPass pass, int index)
        {
            var uniforms = new DrawUniforms
            {
                Layer = _frame[index], TargetWidth = _width, TargetHeight = _height, Weight = _lab._weight,
            };
            commands.PushVertexUniformData(uniforms);
            commands.PushFragmentUniformData(uniforms);
            pass.DrawPrimitives(6, 1, 0, 0);
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            foreach (Handle shown in _shown)
                _context.Hide(shown);
            foreach (Mw.GraphicsPipeline pipeline in (Mw.GraphicsPipeline[])[_blend, _oit, _compositePipe, _exactPipe, _backPipe])
                pipeline.Dispose();
            foreach (Mw.Shader shader in (Mw.Shader[])[_layerVertex, _blendShade, _oitShade, _fullVertex, _composite, _exact, _back])
                shader.Dispose();
            _sampler.Dispose();
            _accum.Dispose();
            _reveal.Dispose();
            foreach (Mw.Texture pane in _panes)
                pane.Dispose();
        }
    }
}
