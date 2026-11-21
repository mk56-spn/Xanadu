using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Rendering;
using Xanadu.Ui.Ecs.Ui;
using Mw = MoonWorks.Graphics;
using Color = Xanadu.Core.Maths.Color;
using Xanadu.Slang;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Radiance cascades (Shaders/radiance_lab.slang): 2D global illumination in
/// screen space. Paint lights and walls on the stage; every lit pixel lights
/// everything it can see, with soft shadows, at a cost that does not grow with
/// the number of lights. Walls bounce what reaches them (last frame's light, so
/// it settles over a few frames). Views show the scene, the jump-flood distance
/// field the rays trace, and each cascade's raw, pre-averaged probe blocks.
/// </summary>
internal sealed class RadianceLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;

    /// <summary>Cascade textures made up front; the base interval decides how many are used.</summary>
    private const int MaxCascades = 8;

    private static readonly string[] Brushes = ["LIGHT", "WALL", "ERASE"];

    /// <summary>
    /// How cascades merge, and how many from the bottom take the bilinear fix:
    /// each ray traced once to every one of the four upper probes, so the two
    /// meet without rings at four times the marches. Rings show most near
    /// lights, where the low cascades answer; above those the fix costs far more
    /// than it shows, so the default stops there.
    /// </summary>
    private static readonly (string Name, int Fixed)[] Merges = [("PLAIN", 0), ("FIX C0-C1", 2), ("FIX ALL", MaxCascades)];

    /// <summary>
    /// The jump flood's first jump: it finds blockers within about twice this,
    /// and every pixel further off is given this distance, which still steps a
    /// ray safely. Four passes fewer than flooding the whole stage, for a few
    /// more steps on long rays through open space.
    /// </summary>
    private const uint FloodReach = 64;
    private static readonly string[] CascadeNames = Enumerable.Range(0, MaxCascades).Select(n => $"cascade {n}").ToArray();

    private readonly LabPanel _ui = new();
    private int _brush;
    private int _merge = 1;
    private int _view;
    private int _cascades = 1;
    private bool _clear, _reset = true;

    private Entity _hue, _bright, _radius, _interval, _exposure, _albedo, _movers, _clearButton, _resetButton, _readout;

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
        _exposure = LabPanel.Slider(panel, "EXPOSURE", 0.5f, 30, 2.5f, "Scales the light before tone mapping", "0.0");
        _albedo = LabPanel.Slider(panel, "WALL ALBEDO", 0, 0.95f, 0.6f, "How much of the light on a wall it bounces on; 0 is no bounce", "0.00");

        _ui.Stepper(panel, "MERGE", () => _merge, i => _merge = i, Merges.Length, i => Merges[i].Name);

        UiNode toggles = LabPanel.Row(panel);
        _movers = LabPanel.Toggle(toggles, "MOVERS ON", "MOVERS OFF", true, 196, "Two orbiting lights, a beat light at 120 BPM and a turning bar");
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
        _ => view - 3 < _cascades ? $"CASCADE {view - 3}  (probe blocks)" : $"CASCADE {view - 3}  (unused)",
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

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct Params
    {
        public Vector4 Size, Cascade, Setup, BrushA, BrushB, BrushC, Look, Extra;
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R16G16B16A16Float;
        private const Mw.TextureFormat SeedFormat = Mw.TextureFormat.R16G16B16A16Float;
        private const Mw.TextureFormat DistanceFormat = Mw.TextureFormat.R16Float;
        private const Mw.TextureFormat ShowFormat = Mw.TextureFormat.R8G8B8A8Unorm;

        private readonly RadianceLab _lab;
        private readonly LabContext _context;
        private readonly Rect2 _area;
        private readonly uint _width, _height;

        /// <summary>Each cascade's probes across and down; its texture is these times 2^n.</summary>
        private readonly (uint X, uint Y)[] _probes = new (uint, uint)[MaxCascades];
        private readonly Mw.GraphicsDevice _device;
        private readonly Texture _paint, _scene, _floodA, _floodB, _distance, _shownTexture;
        private readonly Texture[] _cascadeTextures = new Texture[MaxCascades];
        private readonly Sampler _sampler;
        private readonly Shader[] _shaders;
        private readonly GraphicsPipeline _demo, _brush, _compose, _floodSeed, _floodStep, _floodDistance, _cascade, _show;
        private readonly Handle _item, _shown;
        private float _time;
        private Vector2 _last;
        private bool _wasHeld;
        private Params _params;

        /// <summary>Whichever flood texture the last jump wrote: nearest blocker in xy, nearest open pixel in zw.</summary>
        private Texture _flood;
        private bool _stroke;

        public Stage(RadianceLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _area = area;
            _device = context.Device;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;
            // Cascade n has a probe every 2^(n+1) px, enough to cover the stage.
            for (int n = 0; n < MaxCascades; n++)
            {
                uint spacing = 2u << n;
                _probes[n] = ((_width + spacing - 1) / spacing, (_height + spacing - 1) / spacing);
            }

            const Mw.TextureUsageFlags usage = Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler;
            _paint = Texture.Create2D(_device, "radiance paint", _width, _height, Format, usage);
            _scene = Texture.Create2D(_device, "radiance scene", _width, _height, Format, usage);
            _floodA = Texture.Create2D(_device, "radiance flood a", _width, _height, SeedFormat, usage);
            _floodB = Texture.Create2D(_device, "radiance flood b", _width, _height, SeedFormat, usage);
            _flood = _floodA;
            _distance = Texture.Create2D(_device, "radiance distance", _width, _height, DistanceFormat, usage);
            for (int n = 0; n < MaxCascades; n++)
            {
                (uint w, uint h) = CascadeSize(n);
                _cascadeTextures[n] = Texture.Create2D(_device, $"radiance cascade {n}", w, h, Format, usage);
            }
            _shownTexture = Texture.Create2D(_device, "radiance shown", _width, _height, ShowFormat, usage);
            // Linear, so cascade 0 blends its probes in one fetch; every other read lands on a texel centre.
            _sampler = Sampler.Create(_device, Mw.SamplerCreateInfo.LinearClamp);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "radiance_lab.slang");
            Shader full = Shader(module, "full", Mw.ShaderStage.Vertex, 0);
            Shader demo = Shader(module, "demo", Mw.ShaderStage.Fragment, 0);
            Shader brush = Shader(module, "brush", Mw.ShaderStage.Fragment, 0);
            Shader scene = Shader(module, "scene", Mw.ShaderStage.Fragment, 3);
            Shader floodSeed = Shader(module, "floodSeed", Mw.ShaderStage.Fragment, 1);
            Shader floodStep = Shader(module, "floodStep", Mw.ShaderStage.Fragment, 1);
            Shader floodDistance = Shader(module, "floodDistance", Mw.ShaderStage.Fragment, 1);
            Shader cascade = Shader(module, "cascade", Mw.ShaderStage.Fragment, 4);
            Shader show = Shader(module, "show", Mw.ShaderStage.Fragment, 4);
            _shaders = [full, demo, brush, scene, floodSeed, floodStep, floodDistance, cascade, show];

            _demo = Pipeline(full, demo, Format);
            _brush = Pipeline(full, brush, Format);
            _compose = Pipeline(full, scene, Format);
            _floodSeed = Pipeline(full, floodSeed, SeedFormat);
            _floodStep = Pipeline(full, floodStep, SeedFormat);
            _floodDistance = Pipeline(full, floodDistance, DistanceFormat);
            _cascade = Pipeline(full, cascade, Format);
            _show = Pipeline(full, show, ShowFormat);

            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            _shown = context.Show(_shownTexture);
            CanvasServer.Current.AddTextureRect(_item, area, _shown);
        }

        /// <summary>Cascade n's texture: its probes, each a 2^n square block of pre-averaged directions.</summary>
        private (uint Width, uint Height) CascadeSize(int n) => (_probes[n].X << n, _probes[n].Y << n);

        private Shader Shader(string module, string entry, Mw.ShaderStage kind, uint samplers) =>
            MoonWorks.Graphics.Shader.Create(_device, SlangShaders.Compile(module, entry, kind == Mw.ShaderStage.Vertex ? "vertex" : "fragment"), "main",
                new Mw.ShaderCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    Stage = kind,
                    NumUniformBuffers = kind == Mw.ShaderStage.Vertex ? 0u : 1u,
                    NumSamplers = samplers,
                });

        private GraphicsPipeline Pipeline(Shader vertex, Shader fragment, Mw.TextureFormat format) =>
            GraphicsPipeline.Create(_device, new Mw.GraphicsPipelineCreateInfo
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

        public void Update(in ToolFrame frame)
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
            int fixedCascades = Math.Min(count, Merges[_lab._merge].Fixed);
            UiText.Set(_lab._readout,
                $"{count} cascades · c0 {_probes[0].X}×{_probes[0].Y} probes × 4 rays · reach {top:0} px · fix on {fixedCascades}");

            _params = new Params
            {
                Size = new Vector4(_width, _height, 0, 0),
                Setup = new Vector4(count, 0, fixedCascades, 0),
                BrushA = new Vector4(from.X, from.Y, local.X, local.Y),
                BrushB = ink,
                BrushC = new Vector4(LabPanel.Value(_lab._radius), LabPanel.Value(_lab._albedo), 0, 0),
                Look = new Vector4(_time, LabPanel.On(_lab._movers) ? 1 : 0, LabPanel.Value(_lab._exposure), _lab._view),
                Extra = new Vector4(0, 0, CascadeSize(0).Width, CascadeSize(0).Height),
            };
        }

        private void FullScreen(Mw.CommandBuffer commands, Texture target, GraphicsPipeline pipeline, Params p,
            params ReadOnlySpan<Texture> inputs)
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

                // The scene reads last frame's light and flood to bounce: start them dark, with no seeds.
                foreach (Texture texture in (Texture[])[_floodA, _floodB, .. _cascadeTextures])
                    commands.EndRenderPass(commands.BeginRenderPass(new Mw.ColorTargetInfo(texture, new Mw.Color(0, 0, 0, 0))));
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
                FullScreen(commands, _scene, _compose, p, _paint, _cascadeTextures[0], _flood);

            // Jump flood: seed, then halve the jump each pass down to one pixel (see FloodReach).
            GpuScope floodScope = commands.Scope("jump flood");
            FullScreen(commands, _floodA, _floodSeed, p, _scene);
            Texture read = _floodA, write = _floodB;
            uint largest = Math.Max(_width, _height);
            for (uint jump = Math.Min(FloodReach, System.Numerics.BitOperations.RoundUpToPowerOf2(largest) / 2); jump >= 1; jump /= 2)
            {
                p.Setup.W = jump;
                FullScreen(commands, write, _floodStep, p, read);
                (read, write) = (write, read);
            }
            _flood = read;
            FullScreen(commands, _distance, _floodDistance, p, read);
            floodScope.Dispose();

            // Cascades from the top down; each adds the one above beyond its own interval.
            int count = _lab._cascades;
            float interval = LabPanel.Value(_lab._interval);
            for (int n = count - 1; n >= 0; n--)
            {
                float start = interval * (MathF.Pow(4, n) - 1) / 3;
                float end = interval * (MathF.Pow(4, n + 1) - 1) / 3;
                p.Cascade = new Vector4(n, 2 << n, start, end);
                p.Setup.Y = n < count - 1 ? 1 : 0;
                (uint w, uint h) = CascadeSize(n);
                p.Size = new Vector4(_width, _height, w, h);
                (uint upperX, uint upperY) = _probes[Math.Min(n + 1, MaxCascades - 1)];
                p.Extra = new Vector4(upperX, upperY, p.Extra.Z, p.Extra.W);
                // The top cascade reads nothing above; any texture but its own target will do.
                Texture upper = n < count - 1 ? _cascadeTextures[n + 1] : _scene;
                using (commands.Scope(CascadeNames[n]))
                    FullScreen(commands, _cascadeTextures[n], _cascade, p, _scene, _distance, upper, _flood);
            }

            int viewed = Math.Clamp(_lab._view - 3, 0, MaxCascades - 1);
            (uint viewedWidth, uint viewedHeight) = CascadeSize(viewed);
            p.Size = new Vector4(_width, _height, viewedWidth, viewedHeight);
            using (commands.Scope("show"))
                FullScreen(commands, _shownTexture, _show, p, _scene, _cascadeTextures[0], _flood, _cascadeTextures[viewed]);
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (GraphicsPipeline pipeline in (GraphicsPipeline[])[_demo, _brush, _compose, _floodSeed, _floodStep, _floodDistance, _cascade, _show])
                pipeline.Dispose();
            foreach (Shader shader in _shaders)
                shader.Dispose();
            _sampler.Dispose();
            foreach (Texture texture in (Texture[])[_paint, _scene, _floodA, _floodB, _distance, _shownTexture, .. _cascadeTextures])
                texture.Dispose();
        }
    }
}
