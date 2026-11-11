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
/// The pointer's path kept on the GPU as a ring of keys, stored only where the
/// path needs them: a key goes in when a straight line from the last one would
/// miss some frame since by more than the tolerance (in pixels, measured at
/// that frame's own time). A still pointer costs nothing, a steady swipe costs
/// two keys, a scribble costs a key per bend. All reading of the path is on the
/// GPU: a compute pass finds a time-delayed ghost (binary search by time), a
/// smoothed follower, and steps a chain of chasers whose state stays in a GPU
/// buffer; instanced quads draw the trail and the keys.
/// </summary>
internal sealed class PointerHistoryLab : ILabExperiment
{
    private const int RingSize = 1024;
    private const int MaxChain = 48;
    private const int StateSize = 2 + MaxChain;
    private const uint Pieces = 8;

    /// <summary>The most frames one key may stand for, so a long still stretch still lands in the ring now and then.</summary>
    private const int MaxSpan = 600;

    private Entity _tolerance, _curves, _indirect, _rate, _spring, _delay, _halfLife, _links, _spacing, _trail, _readout, _span;

    public string Name => "POINTER HISTORY";

    public string[] Notes =>
    [
        "The path is a GPU ring of keys, stored only where a straight",
        "line stops fitting; compute and vertex shaders do the rest,",
        "and the compute pass sizes the trail's draws (indirect).",
        "Off the stage, the pointer runs a figure of eight with pauses.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _tolerance = LabPanel.Slider(panel, "TOLERANCE", 0.05f, 10, 0.5f,
            "How far, in pixels, the stored path may miss any frame before a new key goes in");
        UiNode flags = LabPanel.Row(panel);
        _curves = LabPanel.Toggle(flags, "CURVES", "LINES", true, 196,
            "Between keys: a cubic through them with time-aware tangents, or straight lines");
        _indirect = LabPanel.Toggle(flags, "INDIRECT", "DIRECT", true, 196,
            "INDIRECT: the compute pass writes how many instances to draw. DIRECT: the CPU draws every stored key and the vertex shader throws away the old ones");
        _trail = LabPanel.Slider(panel, "TRAIL", 0.5f, 20, 6, "How many seconds of path to draw", "0.0");
        _delay = LabPanel.Slider(panel, "GHOST DELAY", 0, 10, 0.5f, "How far behind the gold ring is, in seconds", "0.00");
        _halfLife = LabPanel.Slider(panel, "SMOOTHING", 0.01f, 1, 0.15f, "Half-life of the green diamond's average over the path, in seconds");
        _rate = LabPanel.Slider(panel, "CHASE RATE", 1, 40, 12, "How fast each link of the chain closes the gap (per second)", "0.0");
        _spring = LabPanel.Slider(panel, "SPRING", 0, 1, 0, "0 = a plain frame-rate independent lerp; above, a damped spring that overshoots more as it rises");
        _spacing = LabPanel.Slider(panel, "LINK GAP", 0, 40, 14, "The most a link may trail the one in front, in pixels (0 = no limit)", "0");
        _links = LabPanel.Slider(panel, "LINKS", 1, MaxChain, 24, "How many links in the chain", "0");
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);
        _span = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);

        panel.Text("White cross: the pointer now. Blue: the stored path, dots", UiType.Small, Palette.TextMuted);
        panel.Text("on its keys. Gold ring: the ghost. Green diamond: the", UiType.Small, Palette.TextMuted);
        panel.Text("smoothed follower. Colours: the chain, state kept on the GPU.", UiType.Small, Palette.TextMuted);
    }

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    /// <summary>One key: x, y in stage pixels, the lab's time it was at, unused.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Key
    {
        public float X, Y, Time, Pad;

        public Key(Vector2 at, float time)
        {
            X = at.X;
            Y = at.Y;
            Time = time;
            Pad = 0;
        }

        public readonly Vector2 At => new(X, Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SimUniforms
    {
        public Key Head;
        public float Newest, Stored, Delta, Curves;
        public float Rate, Spring, Delay, HalfLife;
        public float Links, Spacing, Reset, Trail;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LookUniforms
    {
        public Key Head;
        public float Newest, Stored, Pad, Curves;
        public float Trail, Links, Width, Height;
    }

    /// <summary>
    /// Decides which frames become keys. It holds the frames since the last key;
    /// each new frame is tried as the end of a straight line from that key, and
    /// if some held frame is off the line by more than the tolerance (at its own
    /// time, so speed changes count as well as turns), the frame before becomes
    /// a key and the line starts again from it.
    /// </summary>
    private sealed class Keyframer
    {
        private readonly List<Key> _since = new();
        private Key _last;
        private bool _started;

        public void Feed(Key frame, float tolerance, List<Key> emitted)
        {
            if (!_started)
            {
                _started = true;
                Emit(frame, emitted);
                return;
            }

            _since.Add(frame);
            if (!Fits(tolerance))
            {
                Key previous = _since[^2];
                Emit(previous, emitted);
                _since.Clear();
                _since.Add(frame);
            }
            else if (_since.Count >= MaxSpan)
            {
                Emit(frame, emitted);
                _since.Clear();
            }
        }

        private void Emit(Key key, List<Key> emitted)
        {
            _last = key;
            emitted.Add(key);
        }

        private bool Fits(float tolerance)
        {
            Key end = _since[^1];
            float span = end.Time - _last.Time;
            if (span <= 0)
                return true;

            float limit = tolerance * tolerance;
            for (int i = 0; i < _since.Count - 1; i++)
            {
                Key frame = _since[i];
                Vector2 expected = _last.At.Lerp(end.At, (frame.Time - _last.Time) / span);
                if (expected.DistanceSquaredTo(frame.At) > limit)
                    return false;
            }

            return true;
        }
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;

        private readonly PointerHistoryLab _lab;
        private readonly LabContext _context;
        private readonly Mw.GraphicsDevice _device;
        private readonly Rect2 _area;
        private readonly uint _width, _height;
        private readonly Mw.Texture _target;
        private readonly Handle _shown, _item;
        private readonly Mw.Buffer _keys, _state, _draws;
        private readonly Mw.TransferBuffer _upload;
        private readonly Mw.ComputePipeline _simulate;
        private readonly Mw.Shader[] _shaders;
        private readonly Mw.GraphicsPipeline _back, _trail, _dots, _markers;

        private readonly Keyframer _keyframer = new();
        private readonly List<Key> _emitted = new();
        private readonly float[] _times = new float[RingSize];
        private Key _head;
        private float _delta;
        private int _newest = -1;
        private long _stored, _frames;
        private bool _reset = true;

        public Stage(PointerHistoryLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _device = context.Device;
            _area = area;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            _target = Mw.Texture.Create2D(_device, "pointer history", _width, _height, Format,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

            _keys = Mw.Buffer.Create<Key>(_device, "pointer history keys",
                Mw.BufferUsageFlags.ComputeStorageRead | Mw.BufferUsageFlags.GraphicsStorageRead, RingSize);
            _state = Mw.Buffer.Create<Key>(_device, "pointer history state",
                Mw.BufferUsageFlags.ComputeStorageRead | Mw.BufferUsageFlags.ComputeStorageWrite | Mw.BufferUsageFlags.GraphicsStorageRead,
                StateSize);
            _draws = Mw.Buffer.Create<Mw.IndirectDrawCommand>(_device, "pointer history draws",
                Mw.BufferUsageFlags.Indirect | Mw.BufferUsageFlags.ComputeStorageWrite, 2);
            // A frame emits at most two keys (the first frame, or a break plus nothing else), but leave room.
            _upload = Mw.TransferBuffer.Create<Key>(_device, "pointer history upload", Mw.TransferBufferUsage.Upload, 8);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "pointer_history_lab.slang");
            _simulate = Mw.ComputePipeline.Create(_device, SlangShaders.Compile(module, "simulate", "compute"), "main",
                new Mw.ComputePipelineCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    NumReadonlyStorageBuffers = 1,
                    NumReadWriteStorageBuffers = 2,
                    NumUniformBuffers = 1,
                    ThreadCountX = 1,
                    ThreadCountY = 1,
                    ThreadCountZ = 1,
                });

            Mw.Shader full = Shader(module, "full", Mw.ShaderStage.Vertex);
            Mw.Shader back = Shader(module, "back", Mw.ShaderStage.Fragment, uniforms: 1);
            Mw.Shader trail = Shader(module, "trailVertex", Mw.ShaderStage.Vertex, storage: 1, uniforms: 1);
            Mw.Shader key = Shader(module, "keyVertex", Mw.ShaderStage.Vertex, storage: 1, uniforms: 1);
            Mw.Shader stroke = Shader(module, "strokeShade", Mw.ShaderStage.Fragment);
            Mw.Shader markers = Shader(module, "markers", Mw.ShaderStage.Fragment, storage: 2, uniforms: 1);
            _shaders = [full, back, trail, key, stroke, markers];

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
                ColorWriteMask = Mw.ColorComponentFlags.RGBA,
            };
            _back = Pipeline(full, back, Mw.ColorTargetBlendState.NoBlend);
            _trail = Pipeline(trail, stroke, premultiplied);
            _dots = Pipeline(key, stroke, premultiplied);
            _markers = Pipeline(full, markers, premultiplied);
        }

        private Mw.Shader Shader(string module, string entry, Mw.ShaderStage stage, uint storage = 0, uint uniforms = 0) =>
            Mw.Shader.Create(_device, SlangShaders.Compile(module, entry, stage == Mw.ShaderStage.Vertex ? "vertex" : "fragment"),
                "main", new Mw.ShaderCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    Stage = stage,
                    NumStorageBuffers = storage,
                    NumUniformBuffers = uniforms,
                });

        private Mw.GraphicsPipeline Pipeline(Mw.Shader vertex, Mw.Shader fragment, Mw.ColorTargetBlendState blend) =>
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
                    ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = Format, BlendState = blend }],
                },
            });

        public void Update(in ToolFrame frame)
        {
            _delta = frame.Delta;

            // The pointer in stage pixels; off the stage, a figure of eight that
            // slows almost to a stop now and then, so stillness shows up too.
            Vector2 local = frame.Mouse - _area.Position;
            if (local.X < 0 || local.Y < 0 || local.X >= _width || local.Y >= _height)
            {
                float s = frame.Time * 1.1f;
                float t = s - 0.95f * MathF.Sin(s);
                local = new Vector2(_width, _height) * 0.5f
                        + new Vector2(MathF.Sin(t) * _width * 0.32f, MathF.Sin(t * 2) * _height * 0.22f);
            }

            _head = new Key(local, frame.Time);
            _frames++;
            _keyframer.Feed(_head, LabPanel.Value(_lab._tolerance), _emitted);
        }

        public void Render(Mw.CommandBuffer commands)
        {
            // The only CPU → GPU traffic: the keys this frame made, usually none.
            if (_emitted.Count > 0)
            {
                Span<Key> mapped = _upload.Map<Key>(cycle: true);
                for (int i = 0; i < _emitted.Count; i++)
                    mapped[i] = _emitted[i];
                _upload.Unmap();

                Mw.CopyPass copy = commands.BeginCopyPass();
                for (int i = 0; i < _emitted.Count; i++)
                {
                    _newest = (_newest + 1) % RingSize;
                    _times[_newest] = _emitted[i].Time;
                    _stored++;
                    copy.UploadToBuffer<Key>(_upload, _keys, (uint)i, (uint)_newest, 1, cycle: false);
                }

                commands.EndCopyPass(copy);
                _emitted.Clear();
            }

            int stored = (int)Math.Min(_stored, RingSize);
            float curves = LabPanel.On(_lab._curves) ? 1 : 0;
            float links = MathF.Round(LabPanel.Value(_lab._links));
            ShowReadout(stored);

            var sim = new SimUniforms
            {
                Head = _head, Newest = _newest, Stored = stored, Delta = MathF.Min(_delta, 0.1f), Curves = curves,
                Rate = LabPanel.Value(_lab._rate), Spring = LabPanel.Value(_lab._spring),
                Delay = LabPanel.Value(_lab._delay), HalfLife = LabPanel.Value(_lab._halfLife),
                Links = links, Spacing = LabPanel.Value(_lab._spacing), Reset = _reset ? 1 : 0,
                Trail = LabPanel.Value(_lab._trail),
            };
            _reset = false;

            Mw.ComputePass compute = commands.BeginComputePass([],
                [new Mw.StorageBufferReadWriteBinding(_state), new Mw.StorageBufferReadWriteBinding(_draws)]);
            compute.BindComputePipeline(_simulate);
            compute.BindStorageBuffers(_keys);
            commands.PushComputeUniformData(sim);
            compute.Dispatch(1, 1, 1);
            commands.EndComputePass(compute);

            var look = new LookUniforms
            {
                Head = _head, Newest = _newest, Stored = stored, Curves = curves,
                Trail = LabPanel.Value(_lab._trail), Links = links, Width = _width, Height = _height,
            };
            Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, Mw.LoadOp.DontCare));
            commands.PushVertexUniformData(look);
            commands.PushFragmentUniformData(look);

            pass.BindGraphicsPipeline(_back);
            pass.DrawPrimitives(3, 1, 0, 0);

            // Indirect: the counts the compute pass just wrote, never seen by the CPU.
            // Direct: every stored segment's pieces, the vertex shader dropping the ones past the trail.
            bool indirect = LabPanel.On(_lab._indirect);
            pass.BindGraphicsPipeline(_trail);
            pass.BindVertexStorageBuffers(_keys);
            if (indirect)
                pass.DrawPrimitivesIndirect(_draws, 0, 1);
            else if (stored > 0)
                pass.DrawPrimitives(6, (uint)stored * Pieces, 0, 0);

            pass.BindGraphicsPipeline(_dots);
            pass.BindVertexStorageBuffers(_keys);
            if (indirect)
                pass.DrawPrimitivesIndirect(_draws, (uint)Marshal.SizeOf<Mw.IndirectDrawCommand>(), 1);
            else if (stored > 0)
                pass.DrawPrimitives(6, (uint)stored, 0, 0);

            pass.BindGraphicsPipeline(_markers);
            pass.BindFragmentStorageBuffers(_keys, _state);
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);
        }

        private void ShowReadout(int stored)
        {
            float ratio = _stored > 0 ? (float)_frames / _stored : 0;
            UiText.Set(_lab._readout, $"{_stored} keys for {_frames} frames · {ratio:0.0}× fewer · {stored * 16 / 1024f:0.0} of {RingSize * 16 / 1024} KB");
            float covered = stored > 0 ? _head.Time - _times[(_newest - (stored - 1) + RingSize) % RingSize] : 0;
            string full = _stored >= RingSize ? "ring full" : "ring filling";
            UiText.Set(_lab._span, $"{full} · it holds the last {covered:0.0} s");
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (Mw.GraphicsPipeline pipeline in (Mw.GraphicsPipeline[])[_back, _trail, _dots, _markers])
                pipeline.Dispose();
            foreach (Mw.Shader shader in _shaders)
                shader.Dispose();
            _simulate.Dispose();
            _upload.Dispose();
            _keys.Dispose();
            _state.Dispose();
            _draws.Dispose();
            _target.Dispose();
        }
    }
}
