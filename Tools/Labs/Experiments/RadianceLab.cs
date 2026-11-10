using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Radiance cascades (Shaders/radiance_lab.slang): 2D global illumination in
/// screen space. Paint lights and walls on the stage; every lit pixel lights
/// everything it can see, with soft shadows, at a cost that does not grow with
/// the number of lights. Views show the scene, the jump-flood distance field
/// the rays trace, and each cascade's raw probe blocks.
/// </summary>
internal sealed class RadianceLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;

    /// <summary>Cascade textures made up front; the base interval decides how many are used.</summary>
    private const int MaxCascades = 8;

    private static readonly string[] Brushes = ["LIGHT", "WALL", "ERASE"];
    private static readonly string[] CascadeNames = Enumerable.Range(0, MaxCascades).Select(n => $"cascade {n}").ToArray();

    private readonly LabPanel _ui = new();
    private int _brush;
    private int _view;
    private int _cascades = 1;
    private bool _clear, _reset = true;

    private Entity _hue, _bright, _radius, _interval, _exposure, _movers, _fix, _clearButton, _resetButton, _readout;

    public string Name => "RADIANCE";

    public string[] Notes =>
    [
        "Radiance cascades: every lit pixel is a light, soft shadows",
        "for free. Paint on the stage with the brush below.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "BRUSH", () => _brush, i => _brush = i, Brushes.Length, i => Brushes[i]);
        _hue = LabPanel.Slider(panel, "HUE", 0, 1, 0.08f, "The light brush's colour");
        _bright = LabPanel.Slider(panel, "BRIGHTNESS", 0.1f, 8, 2.5f, "What the light brush emits", "0.0");
        _radius = LabPanel.Slider(panel, "BRUSH SIZE", 2, 60, 10, "The brush radius in pixels", "0");
        _interval = LabPanel.Slider(panel, "BASE INTERVAL", 0.5f, 16, 2, "Cascade 0's ray length in pixels; each cascade above reaches four times further", "0.0");
        _exposure = LabPanel.Slider(panel, "EXPOSURE", 0.5f, 30, 6, "Scales the light before tone mapping", "0.0");

        UiNode toggles = LabPanel.Row(panel);
        _movers = LabPanel.Toggle(toggles, "MOVERS ON", "MOVERS OFF", true, 196, "Two orbiting lights, a beat light at 120 BPM and a turning bar");
        _fix = LabPanel.Toggle(toggles, "BILINEAR FIX", "PLAIN MERGE", true, 196,
            "Fix: each ray runs from this probe to the matching upper probe's interval, so the cascades meet without rings (16 rays a texel instead of 1)");
        _clearButton = toggles.Button("CLEAR", UiType.Caption).Size(96, RowHeight).Tooltip("Empty the painted layer");
        _resetButton = toggles.Button("DEMO", UiType.Caption).Size(96, RowHeight).Tooltip("Put the starting walls and lights back");

        _ui.Stepper(panel, "VIEW", () => _view, i => _view = i, 3 + MaxCascades, ViewName);
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);
    }

    private string ViewName(int view) => view switch
    {
        0 => "LIT",
        1 => "SCENE  (emission and walls)",
        2 => "DISTANCE FIELD  (jump flood)",
        _ => view - 3 < _cascades ? $"CASCADE {view - 3}  (raw probe blocks)" : $"CASCADE {view - 3}  (unused)",
    };

    private void Poll()
    {
        _ui.PollSteppers();
        if (_ui.Clicked(_clearButton))
            _clear = true;
        if (_ui.Clicked(_resetButton))
            _reset = true;
        _ui.ShowSteppers();
    }

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct Params
    {
        public Vector4 Size, Cascade, Setup, BrushA, BrushB, BrushC, Look, Extra;
    }

    private sealed class Stage : ILabStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R16G16B16A16Float;
        private const Mw.TextureFormat ShowFormat = Mw.TextureFormat.R8G8B8A8Unorm;

        private readonly RadianceLab _lab;
        private readonly LabContext _context;
        private readonly Rect2 _area;
        private readonly uint _width, _height, _cascadeWidth, _cascadeHeight;
        private readonly Mw.GraphicsDevice _device;
        private readonly Mw.Texture _paint, _scene, _floodA, _floodB, _shownTexture;
        private readonly Mw.Texture[] _cascadeTextures = new Mw.Texture[MaxCascades];
        private readonly Mw.Sampler _sampler;
        private readonly Mw.Shader[] _shaders;
        private readonly Mw.GraphicsPipeline _demo, _brush, _compose, _floodSeed, _floodStep, _cascade, _show;
        private readonly Handle _item, _shown;
        private float _time;
        private Vector2 _last;
        private bool _wasHeld;
        private Params _params;
        private bool _stroke;

        public Stage(RadianceLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _area = area;
            _device = context.Device;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;
            // Every cascade's probes must tile it exactly, up to the top one's 2^MaxCascades blocks.
            const uint top = 1u << MaxCascades;
            _cascadeWidth = (_width + top - 1) / top * top;
            _cascadeHeight = (_height + top - 1) / top * top;

            const Mw.TextureUsageFlags usage = Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler;
            _paint = Mw.Texture.Create2D(_device, "radiance paint", _width, _height, Format, usage);
            _scene = Mw.Texture.Create2D(_device, "radiance scene", _width, _height, Format, usage);
            _floodA = Mw.Texture.Create2D(_device, "radiance flood a", _width, _height, Format, usage);
            _floodB = Mw.Texture.Create2D(_device, "radiance flood b", _width, _height, Format, usage);
            for (int i = 0; i < MaxCascades; i++)
                _cascadeTextures[i] = Mw.Texture.Create2D(_device, $"radiance cascade {i}", _cascadeWidth, _cascadeHeight, Format, usage);
            _shownTexture = Mw.Texture.Create2D(_device, "radiance shown", _width, _height, ShowFormat, usage);
            _sampler = Mw.Sampler.Create(_device, Mw.SamplerCreateInfo.PointClamp);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "radiance_lab.slang");
            Mw.Shader full = Shader(module, "full", Mw.ShaderStage.Vertex, 0);
            Mw.Shader demo = Shader(module, "demo", Mw.ShaderStage.Fragment, 0);
            Mw.Shader brush = Shader(module, "brush", Mw.ShaderStage.Fragment, 0);
            Mw.Shader scene = Shader(module, "scene", Mw.ShaderStage.Fragment, 1);
            Mw.Shader floodSeed = Shader(module, "floodSeed", Mw.ShaderStage.Fragment, 1);
            Mw.Shader floodStep = Shader(module, "floodStep", Mw.ShaderStage.Fragment, 1);
            Mw.Shader cascade = Shader(module, "cascade", Mw.ShaderStage.Fragment, 3);
            Mw.Shader show = Shader(module, "show", Mw.ShaderStage.Fragment, 4);
            _shaders = [full, demo, brush, scene, floodSeed, floodStep, cascade, show];

            _demo = Pipeline(full, demo, Format);
            _brush = Pipeline(full, brush, Format);
            _compose = Pipeline(full, scene, Format);
            _floodSeed = Pipeline(full, floodSeed, Format);
            _floodStep = Pipeline(full, floodStep, Format);
            _cascade = Pipeline(full, cascade, Format);
            _show = Pipeline(full, show, ShowFormat);

            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            _shown = context.Show(_shownTexture);
            CanvasServer.Current.AddTextureRect(_item, area, _shown);
        }

        private Mw.Shader Shader(string module, string entry, Mw.ShaderStage kind, uint samplers) =>
            Mw.Shader.Create(_device, SlangShaders.Compile(module, entry, kind == Mw.ShaderStage.Vertex ? "vertex" : "fragment"), "main",
                new Mw.ShaderCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    Stage = kind,
                    NumUniformBuffers = kind == Mw.ShaderStage.Vertex ? 0u : 1u,
                    NumSamplers = samplers,
                });

        private Mw.GraphicsPipeline Pipeline(Mw.Shader vertex, Mw.Shader fragment, Mw.TextureFormat format) =>
            Mw.GraphicsPipeline.Create(_device, new Mw.GraphicsPipelineCreateInfo
            {
                VertexShader = vertex,
                FragmentShader = fragment,
                VertexInputState = Mw.VertexInputState.Empty,
                PrimitiveType = Mw.PrimitiveType.TriangleList,
                RasterizerState = Mw.RasterizerState.CCW_CullNone,
                MultisampleState = Mw.MultisampleState.None,
                DepthStencilState = Mw.DepthStencilState.Disable,
                TargetInfo = new Mw.GraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = format, BlendState = Mw.ColorTargetBlendState.NoBlend }],
                },
            });

        public void Update(in LabFrame frame)
        {
            _lab.Poll();
            _time += frame.Delta;

            // The fewest cascades whose last interval reaches across the stage.
            float interval = LabPanel.Value(_lab._interval);
            float diagonal = MathF.Sqrt(_width * (float)_width + _height * (float)_height);
            int count = 1;
            while (count < MaxCascades && interval * (MathF.Pow(4, count) - 1) / 3 < diagonal)
                count++;
            _lab._cascades = count;

            Vector2 local = frame.Mouse - _area.Position;
            bool inside = _area.HasPoint(frame.Mouse);
            bool started = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;
            if (started)
                _stroke = inside;
            if (!frame.Held)
                _stroke = false;

            Color light = Color.FromHsv(LabPanel.Value(_lab._hue), 0.75f, 1) * LabPanel.Value(_lab._bright);
            Vector4 ink = _lab._brush switch
            {
                0 => new Vector4(light.R, light.G, light.B, 1),
                1 => new Vector4(0, 0, 0, 1),
                _ => Vector4.Zero,
            };
            Vector2 from = started ? local : _last;
            _last = local;

            float top = interval * (MathF.Pow(4, count) - 1) / 3;
            UiText.Set(_lab._readout,
                $"{count} cascades · c0 {_cascadeWidth / 2}×{_cascadeHeight / 2} probes × 4 dirs · reach {top:0} px · {(LabPanel.On(_lab._fix) ? 16 : 1)} rays/texel");

            _params = new Params
            {
                Size = new Vector4(_width, _height, _cascadeWidth, _cascadeHeight),
                Setup = new Vector4(count, 0, LabPanel.On(_lab._fix) ? 1 : 0, 0),
                BrushA = new Vector4(from.X, from.Y, local.X, local.Y),
                BrushB = ink,
                BrushC = new Vector4(LabPanel.Value(_lab._radius), 0, 0, 0),
                Look = new Vector4(_time, LabPanel.On(_lab._movers) ? 1 : 0, LabPanel.Value(_lab._exposure), _lab._view),
                Extra = new Vector4(Math.Max(0, _lab._view - 3), 0, 0, 0),
            };
        }

        private void FullScreen(Mw.CommandBuffer commands, Mw.Texture target, Mw.GraphicsPipeline pipeline, Params p,
            params ReadOnlySpan<Mw.Texture> inputs)
        {
            Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(target, Mw.LoadOp.DontCare));
            pass.BindGraphicsPipeline(pipeline);
            if (inputs.Length > 0)
            {
                Span<Mw.TextureSamplerBinding> bindings = stackalloc Mw.TextureSamplerBinding[inputs.Length];
                for (int i = 0; i < inputs.Length; i++)
                    bindings[i] = new Mw.TextureSamplerBinding(inputs[i], _sampler);
                pass.BindFragmentSamplers(bindings);
            }
            commands.PushFragmentUniformData(p);
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);
        }

        public void Render(Mw.CommandBuffer commands)
        {
            Params p = _params;

            // The painted layer persists between frames: cleared, seeded with the demo, or brushed into.
            GpuScope paintScope = commands.Scope("paint");
            if (_lab._clear || _lab._reset)
            {
                Mw.RenderPass clear = commands.BeginRenderPass(new Mw.ColorTargetInfo(_paint, new Mw.Color(0, 0, 0, 0)));
                if (_lab._reset)
                {
                    clear.BindGraphicsPipeline(_demo);
                    commands.PushFragmentUniformData(p);
                    clear.DrawPrimitives(3, 1, 0, 0);
                }
                commands.EndRenderPass(clear);
                _lab._clear = _lab._reset = false;
            }

            if (_stroke)
            {
                Mw.RenderPass paint = commands.BeginRenderPass(new Mw.ColorTargetInfo(_paint, Mw.LoadOp.Load));
                paint.BindGraphicsPipeline(_brush);
                commands.PushFragmentUniformData(p);
                paint.DrawPrimitives(3, 1, 0, 0);
                commands.EndRenderPass(paint);
            }

            paintScope.Dispose();
            using (commands.Scope("scene"))
                FullScreen(commands, _scene, _compose, p, _paint);

            // Jump flood: seed, then halve the jump each pass down to one pixel.
            GpuScope floodScope = commands.Scope("jump flood");
            FullScreen(commands, _floodA, _floodSeed, p, _scene);
            Mw.Texture read = _floodA, write = _floodB;
            uint largest = Math.Max(_width, _height);
            for (uint jump = System.Numerics.BitOperations.RoundUpToPowerOf2(largest) / 2; jump >= 1; jump /= 2)
            {
                p.Setup.W = jump;
                FullScreen(commands, write, _floodStep, p, read);
                (read, write) = (write, read);
            }
            Mw.Texture flood = read;
            floodScope.Dispose();

            // Cascades from the top down; each adds the one above beyond its own interval.
            int count = _lab._cascades;
            float interval = LabPanel.Value(_lab._interval);
            for (int n = count - 1; n >= 0; n--)
            {
                float start = interval * (MathF.Pow(4, n) - 1) / 3;
                float end = interval * (MathF.Pow(4, n + 1) - 1) / 3;
                p.Cascade = new Vector4(n, 1 << (n + 1), start, end);
                p.Setup.Y = n < count - 1 ? 1 : 0;
                // The top cascade reads nothing above; any texture but its own target will do.
                Mw.Texture upper = n < count - 1 ? _cascadeTextures[n + 1] : _scene;
                using (commands.Scope(CascadeNames[n]))
                    FullScreen(commands, _cascadeTextures[n], _cascade, p, _scene, flood, upper);
            }

            int viewed = Math.Clamp(_lab._view - 3, 0, MaxCascades - 1);
            using (commands.Scope("show"))
                FullScreen(commands, _shownTexture, _show, p, _scene, _cascadeTextures[0], flood, _cascadeTextures[viewed]);
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (Mw.GraphicsPipeline pipeline in (Mw.GraphicsPipeline[])[_demo, _brush, _compose, _floodSeed, _floodStep, _cascade, _show])
                pipeline.Dispose();
            foreach (Mw.Shader shader in _shaders)
                shader.Dispose();
            _sampler.Dispose();
            foreach (Mw.Texture texture in (Mw.Texture[])[_paint, _scene, _floodA, _floodB, _shownTexture, .. _cascadeTextures])
                texture.Dispose();
        }
    }
}
