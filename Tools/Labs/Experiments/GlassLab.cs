using System;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Liquid-glass panels stacked over a busy backdrop, each seeing what's under
/// it, lower panels included, without copying the whole screen per panel.
/// Panels draw bottom to top onto one target; before each, only its region
/// (its footprint plus room for the rim's bend and the frost) is blitted into
/// that panel's own small copy, and the copy's mips are blitted down from it.
/// FULL COPY does it the obvious way for comparison: the whole target per panel.
/// </summary>
internal sealed class GlassLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;
    private const int MaxPanels = 8;

    /// <summary>Room round a panel its copy also takes: the furthest the rim's bend and the frost read past its edge.</summary>
    private const int Margin = 48;

    /// <summary>How far round a panel its shadow reaches, so the panel's quad covers it.</summary>
    private const float ShadowMargin = 36;

    private static readonly string[] Modes = ["REGION COPY", "FULL COPY"];

    private static readonly (Vector2 Home, Vector2 Size, float Radius, Color Tint)[] Layout =
    [
        (new(0.30f, 0.32f), new(460, 280), 36, new(0.55f, 0.75f, 1f)),
        (new(0.52f, 0.44f), new(320, 320), 160, new(1f, 0.6f, 0.75f)),
        (new(0.42f, 0.62f), new(560, 150), 75, new(0.7f, 1f, 0.75f)),
        (new(0.66f, 0.58f), new(300, 380), 44, new(1f, 0.85f, 0.5f)),
        (new(0.55f, 0.30f), new(380, 230), 28, new(0.8f, 0.7f, 1f)),
        (new(0.25f, 0.70f), new(260, 200), 30, new(0.6f, 1f, 1f)),
        (new(0.75f, 0.28f), new(240, 240), 120, new(1f, 0.7f, 0.5f)),
        (new(0.40f, 0.20f), new(420, 120), 60, new(0.9f, 0.9f, 1f)),
    ];

    private readonly LabPanel _ui = new();
    private int _count = 5, _mode;
    private Entity _bendSlider, _bevelSlider, _frostSlider, _splitSlider, _tintSlider, _shadowSlider;
    private Entity _animateToggle, _regionsToggle, _contentToggle, _readout, _ratio;

    public string Name => "GLASS";

    public string[] Notes =>
    [
        "Liquid-glass panels, stacked: each sees what's under it, lower panels too.",
        "Each copies only its own footprint before it draws. Drag a panel to move it.",
    ];

    // ---- panel --------------------------------------------------------------

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "PANELS", () => _count - 1, i => _count = i + 1, MaxPanels, i => (i + 1).ToString());
        _ui.Stepper(panel, "COPY", () => _mode, i => _mode = i, Modes.Length, i => Modes[i]);
        _bendSlider = LabPanel.Slider(panel, "RIM BEND", -40, 40, 22, "How far the rim bends the view, in pixels (below 0 it pushes the inside out)", "0");
        _bevelSlider = LabPanel.Slider(panel, "RIM WIDTH", 4, 80, 26, "How wide the rounded rim is before the top goes flat", "0");
        _frostSlider = LabPanel.Slider(panel, "FROST", 0, 32, 6, "How far the glass blurs what's under it, in pixels", "0");
        _splitSlider = LabPanel.Slider(panel, "COLOUR SPLIT", 0, 0.5f, 0.15f, "Red and blue bend a little more and less than green");
        _tintSlider = LabPanel.Slider(panel, "TINT", 0, 0.6f, 0.12f, "How much of each panel's own colour the glass takes on");
        _shadowSlider = LabPanel.Slider(panel, "SHADOW", 0, 0.8f, 0.3f, "The drop shadow's strength");

        UiNode flags = LabPanel.Row(panel);
        _animateToggle = LabPanel.Toggle(flags, "ANIMATE", "ANIMATE", true, 140, "Let the panels and the backdrop drift");
        _contentToggle = LabPanel.Toggle(flags, "CONTENT", "CONTENT", true, 140, "Draw a title, text and an icon on each panel");
        _regionsToggle = LabPanel.Toggle(flags, "SHOW REGIONS", "SHOW REGIONS", false, 180, "Outline what each panel copies");

        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);
        _ratio = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);

        panel.Text("Panels draw bottom to top onto one target. Before each one,", UiType.Small, Palette.TextMuted);
        panel.Text("its footprint plus a margin for the bend and the frost is", UiType.Small, Palette.TextMuted);
        panel.Text("blitted into its own small copy, then mips blitted down from", UiType.Small, Palette.TextMuted);
        panel.Text("that. The copy holds the backdrop and every panel already", UiType.Small, Palette.TextMuted);
        panel.Text("drawn, so a panel shows the ones below it bent and frosted.", UiType.Small, Palette.TextMuted);
        panel.Text("Cost follows the panels' area, not the screen's × panels.", UiType.Small, Palette.TextMuted);
    }

    private void Poll()
    {
        _ui.PollSteppers();
        _ui.ShowSteppers();
    }

    // ---- stage --------------------------------------------------------------

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct GlassUniforms
    {
        public Vector4 Panel, Region, Look, More, Tint, Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SceneUniforms
    {
        public Vector4 Target;
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;
        private const Mw.TextureUsageFlags Usage = Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler;

        private readonly GlassLab _lab;
        private readonly LabContext _context;
        private readonly Mw.GraphicsDevice _device;
        private readonly Rect2 _area;
        private readonly uint _width, _height;
        private readonly Texture _target, _full;
        private readonly Texture[] _copies = new Texture[MaxPanels];
        private readonly Vector2[] _offsets = new Vector2[MaxPanels];
        private readonly Vector4[] _rects = new Vector4[MaxPanels];
        private readonly Vector4[] _regions = new Vector4[MaxPanels];
        private readonly Sampler _sampler;
        private readonly Shader _fullVertex, _backdrop, _panelVertex, _panel, _regionVertex, _region;
        private readonly GraphicsPipeline _backdropPipe, _panelPipe, _regionPipe;
        private readonly Handle _item, _shown;
        private float _time;
        private int _held = -1;
        private Vector2 _grab;

        public Stage(GlassLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _device = context.Device;
            _area = area;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            _target = Texture.Create2D(_device, "glass lab target", _width, _height, Format, Usage);
            _full = Texture.Create2D(_device, "glass lab full copy", _width, _height, Format, Usage, Levels(_width, _height));
            for (int i = 0; i < MaxPanels; i++)
            {
                // A copy is the panel's size plus the margin all round, so a region never needs resizing.
                Vector2 size = Layout[i].Size + new Vector2(Margin * 2);
                uint w = Math.Min((uint)size.X, _width), h = Math.Min((uint)size.Y, _height);
                _copies[i] = Texture.Create2D(_device, "glass lab copy " + i, w, h, Format, Usage, Levels(w, h));
            }

            _sampler = Sampler.Create(_device, Mw.SamplerCreateInfo.LinearClamp);
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            _shown = context.Show(_target);
            CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "glass_lab.slang");
            _fullVertex = Compile(module, "full", "vertex", Mw.ShaderStage.Vertex);
            _backdrop = Compile(module, "backdropShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);
            _panelVertex = Compile(module, "panelVertex", "vertex", Mw.ShaderStage.Vertex, uniforms: 1);
            _panel = Compile(module, "panelShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1, samplers: 1);
            _regionVertex = Compile(module, "regionVertex", "vertex", Mw.ShaderStage.Vertex, uniforms: 1);
            _region = Compile(module, "regionShade", "fragment", Mw.ShaderStage.Fragment, uniforms: 1);

            Mw.ColorTargetBlendState premultiplied = new()
            {
                EnableBlend = true,
                SrcColorBlendFactor = Mw.BlendFactor.One,
                DstColorBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = Mw.BlendOp.Add,
                SrcAlphaBlendFactor = Mw.BlendFactor.One,
                DstAlphaBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = Mw.BlendOp.Add,
                EnableColorWriteMask = true,
                ColorWriteMask = Mw.ColorComponentFlags.RGB,
            };
            _backdropPipe = Create(_fullVertex, _backdrop, Mw.ColorTargetBlendState.NoBlend);
            _panelPipe = Create(_panelVertex, _panel, premultiplied);
            _regionPipe = Create(_regionVertex, _region, premultiplied);
        }

        /// <summary>A full mip chain down to one pixel.</summary>
        private static uint Levels(uint w, uint h) => (uint)Math.Floor(Math.Log2(Math.Max(w, h))) + 1;

        private Shader Compile(string module, string entry, string stage, Mw.ShaderStage kind, uint uniforms = 0, uint samplers = 0) =>
            Shader.Create(_device, SlangShaders.Compile(module, entry, stage), "main", new Mw.ShaderCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                Stage = kind,
                NumUniformBuffers = uniforms,
                NumSamplers = samplers,
            });

        private GraphicsPipeline Create(Shader vertex, Shader fragment, Mw.ColorTargetBlendState blend) =>
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
                    ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = Format, BlendState = blend }],
                },
            });

        public void Update(in ToolFrame frame)
        {
            _lab.Poll();
            if (LabPanel.On(_lab._animateToggle))
                _time += frame.Delta;

            int count = _lab._count;
            for (int i = 0; i < count; i++)
                _rects[i] = PanelRect(i);

            // Drag: the topmost panel under the pointer when it goes down follows it until it lets go.
            Vector2 local = frame.Mouse - _area.Position;
            if (!frame.Held)
                _held = -1;
            else if (_held < 0 && local.X >= 0 && local.Y >= 0 && local.X < _width && local.Y < _height)
            {
                _held = int.MaxValue;
                for (int i = count - 1; i >= 0; i--)
                {
                    Vector4 r = _rects[i];
                    if (MathF.Abs(local.X - r.X) <= r.Z / 2 && MathF.Abs(local.Y - r.Y) <= r.W / 2)
                    {
                        _held = i;
                        _grab = local - _offsets[i];
                        break;
                    }
                }
            }

            if (_held >= 0 && _held < count)
            {
                _offsets[_held] = local - _grab;
                _rects[_held] = PanelRect(_held);
            }

            // Each panel's region: its footprint plus the margin, slid (not cut) to stay on the target,
            // so it's always exactly its copy's size and the copy's mips line up with it.
            long copied = 0;
            for (int i = 0; i < count; i++)
            {
                Vector4 r = _rects[i];
                float w = _copies[i].Width, h = _copies[i].Height;
                float x = Math.Clamp(MathF.Round(r.X - w / 2), 0, _width - w);
                float y = Math.Clamp(MathF.Round(r.Y - h / 2), 0, _height - h);
                _regions[i] = new Vector4(x, y, w, h);
                copied += (long)(w * h);
            }

            long full = (long)_width * _height * count;
            long shown = _lab._mode == 0 ? copied : full;
            UiText.Set(_lab._readout,
                $"copied a frame: {shown / 1000:N0} k px  plus a third again for mips in {count} blits");
            UiText.Set(_lab._ratio,
                $"region copies {copied / 1000:N0} k px vs full copies {full / 1000:N0} k px: {100.0 * copied / full:0}%");
        }

        /// <summary>Where panel <paramref name="i"/> sits this frame: centre and size, target pixels.</summary>
        private Vector4 PanelRect(int i)
        {
            var (home, size, _, _) = Layout[i];
            float t = _time * 0.35f + i * 1.9f;
            Vector2 drift = new Vector2(MathF.Sin(t), MathF.Cos(t * 0.8f)) * 40;
            Vector2 centre = home * new Vector2(_width, _height) + drift + _offsets[i];
            return new Vector4(centre.X, centre.Y, size.X, size.Y);
        }

        public void Render(Mw.CommandBuffer commands)
        {
            int count = _lab._count;
            bool regionCopy = _lab._mode == 0;
            var targetInfo = new Vector4(_width, _height, _time, ShadowMargin);

            Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, Mw.LoadOp.DontCare, cycle: true));
            pass.BindGraphicsPipeline(_backdropPipe);
            commands.PushFragmentUniformData(new SceneUniforms { Target = targetInfo });
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);

            for (int i = 0; i < count; i++)
            {
                // What's under this panel now: the backdrop and every panel drawn so far.
                Texture copy = regionCopy ? _copies[i] : _full;
                Vector4 region = regionCopy ? _regions[i] : new Vector4(0, 0, _width, _height);
                Blit(commands, _target, 0, (uint)region.X, (uint)region.Y, copy, 0, copy.Width, copy.Height, cycle: true);
                for (uint level = 1; level < copy.LevelCount; level++)
                {
                    uint w = Math.Max(copy.Width >> (int)level, 1), h = Math.Max(copy.Height >> (int)level, 1);
                    Blit(commands, copy, level - 1, 0, 0, copy, level, w, h, cycle: false);
                }

                pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, Mw.LoadOp.Load));
                pass.BindGraphicsPipeline(_panelPipe);
                pass.BindFragmentSamplers(new Mw.TextureSamplerBinding(copy, _sampler));
                GlassUniforms uniforms = Uniforms(i, region, targetInfo);
                commands.PushVertexUniformData(uniforms);
                commands.PushFragmentUniformData(uniforms);
                pass.DrawPrimitives(6, 1, 0, 0);
                commands.EndRenderPass(pass);
            }

            if (LabPanel.On(_lab._regionsToggle))
            {
                pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, Mw.LoadOp.Load));
                pass.BindGraphicsPipeline(_regionPipe);
                for (int i = 0; i < count; i++)
                {
                    GlassUniforms uniforms = Uniforms(i, _regions[i], targetInfo);
                    commands.PushVertexUniformData(uniforms);
                    commands.PushFragmentUniformData(uniforms);
                    pass.DrawPrimitives(6, 1, 0, 0);
                }

                commands.EndRenderPass(pass);
            }
        }

        private GlassUniforms Uniforms(int i, Vector4 region, Vector4 target)
        {
            var (_, _, radius, tint) = Layout[i];
            return new GlassUniforms
            {
                Panel = _rects[i],
                Region = region,
                Look = new Vector4(radius, LabPanel.Value(_lab._bevelSlider), LabPanel.Value(_lab._bendSlider),
                    LabPanel.Value(_lab._frostSlider)),
                More = new Vector4(LabPanel.Value(_lab._splitSlider), LabPanel.Value(_lab._tintSlider),
                    LabPanel.Value(_lab._shadowSlider), _held == i ? 1 : 0),
                Tint = new Vector4(tint.R, tint.G, tint.B, LabPanel.On(_lab._contentToggle) ? 1 : 0),
                Target = target,
            };
        }

        /// <summary>Copies (and scales) a rectangle of one texture's mip onto the whole of another's.</summary>
        private static void Blit(Mw.CommandBuffer commands, Texture from, uint fromLevel, uint x, uint y,
            Texture to, uint toLevel, uint w, uint h, bool cycle)
        {
            uint fw = fromLevel == 0 && from != to ? w : Math.Max(from.Width >> (int)fromLevel, 1);
            uint fh = fromLevel == 0 && from != to ? h : Math.Max(from.Height >> (int)fromLevel, 1);
            commands.Blit(new Mw.BlitInfo
            {
                Source = new Mw.BlitRegion { Texture = from.Handle, MipLevel = fromLevel, X = x, Y = y, W = fw, H = fh },
                Destination = new Mw.BlitRegion { Texture = to.Handle, MipLevel = toLevel, W = w, H = h },
                LoadOp = Mw.LoadOp.DontCare,
                Filter = Mw.Filter.Linear,
                Cycle = cycle,
            });
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (GraphicsPipeline pipeline in (GraphicsPipeline[])[_backdropPipe, _panelPipe, _regionPipe])
                pipeline.Dispose();
            foreach (Shader shader in (Shader[])[_fullVertex, _backdrop, _panelVertex, _panel, _regionVertex, _region])
                shader.Dispose();
            _sampler.Dispose();
            _target.Dispose();
            _full.Dispose();
            foreach (Texture copy in _copies)
                copy.Dispose();
        }
    }
}
