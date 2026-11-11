using System;
using System.IO;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using MoonWorks.Input;
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
/// An RTS that lives on the GPU (Shaders/rts_lab.slang). Five armies of nine
/// unit types fight over thirty sites; you command red. The CPU makes the
/// buffers and pipelines, reads the panel and the raw mouse and keys, and
/// records the passes. It never sees a unit: the grid, targeting, damage,
/// respawns, captures, squad AI, pathfinding, camera, selection, orders,
/// culling, the draw and dispatch sizes and the scoreboard are all GPU work.
/// </summary>
internal sealed class RtsLab : ILabExperiment
{
    /// <summary>Whole twenty-unit mixes for every squad of every army.</summary>
    private static readonly int[] Counts = [6_000, 12_000, 24_000, 48_000, 96_000, 192_000, 384_000, 768_000, 1_536_000];

    private const uint MaxUnits = 1_536_000;
    private const int Cells = 90 * 54;
    private const int Sites = 30, Orders = 8, Fields = Sites + Orders;
    private const uint Fx = 8192;

    /// <summary>Bytes for the Game struct; generously over its std430 size (about 300 KB).</summary>
    private const uint GameBytes = 1 << 20;

    private readonly LabPanel _ui = new();
    private int _count = 2;
    private int _shownCount = -1;
    private bool _restart = true;
    private Entity _speed, _view, _restartButton;

    public string Name => "GPU RTS";

    public string[] Notes =>
    [
        "Five armies over thirty sites, all on the GPU: grid, targets,",
        "damage (atomics), respawns, captures, squad AI, flow fields,",
        "camera, selection, orders, culling and the HUD. The CPU sends",
        "the mouse, the keys and these buttons, and records passes.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "UNITS", () => _count, i => _count = i, Counts.Length, i => Counts[i].ToString("N0"));
        _speed = LabPanel.Slider(panel, "SPEED", 0, 8, 1, "Battle seconds per second; 0 pauses (the camera and orders still work)", "0.0");
        UiNode row = LabPanel.Row(panel);
        _view = LabPanel.Toggle(row, "FLOW FIELD", "BATTLE", false, 220,
            "FLOW FIELD tints the ground with the path field toward the site nearest the pointer");
        _restartButton = row.Button("RESTART", UiType.Caption).Size(160, LabPanel.RowHeight);

        panel.Text("You are red. Left drag selects, right click orders a move", UiType.Small, Palette.TextMuted);
        panel.Text("(the map or the minimap); shift adds, Esc drops. Wheel or", UiType.Small, Palette.TextMuted);
        panel.Text("Q/E zooms, WASD or the middle button pans, F fits, 1-5", UiType.Small, Palette.TextMuted);
        panel.Text("jumps to an army, the minimap drags the view.", UiType.Small, Palette.TextMuted);
        panel.Text("Scoreboard: units up (bright) and allowed (dim) of the", UiType.Small, Palette.TextMuted);
        panel.Text("army's slots, then units up, kills, sites held. Below,", UiType.Small, Palette.TextMuted);
        panel.Text("your selection. Sites: hexagon base, square factory,", UiType.Small, Palette.TextMuted);
        panel.Text("triangle firebase, diamond barracks, circle command;", UiType.Small, Palette.TextMuted);
        panel.Text("each raises its own unit types, so ground matters.", UiType.Small, Palette.TextMuted);
    }

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area)
    {
        _restart = true;
        return new Stage(this, context, parent, area);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Frame
    {
        public float Time, Dt, FrameDt, Steps;
        public float Width, Height, MouseX, MouseY;
        public uint Count, Buttons, Keys, View;
        public int Wheel;
        public uint Pad0, Pad1, Pad2;
    }

    /// <summary>The raw keys the GPU reads, in the bit order rts_lab.slang names them.</summary>
    private static readonly KeyCode[][] KeyBits =
    [
        [KeyCode.W], [KeyCode.A], [KeyCode.S], [KeyCode.D],
        [KeyCode.Up], [KeyCode.Left], [KeyCode.Down], [KeyCode.Right],
        [KeyCode.Q], [KeyCode.E], [KeyCode.F], [KeyCode.LeftShift, KeyCode.RightShift], [KeyCode.Escape],
        [KeyCode.D1], [KeyCode.D2], [KeyCode.D3], [KeyCode.D4], [KeyCode.D5],
    ];

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;
        private const uint Group = 64;

        /// <summary>In-place relaxations of the flow fields a frame; cheap once they settle.</summary>
        private const int Relaxations = 12;

        /// <summary>The longest battle step; a slow frame takes more steps instead.</summary>
        private const float MaxStep = 1 / 30f;

        private const int MaxSteps = 8;

        private readonly RtsLab _lab;
        private readonly LabContext _context;
        private readonly Mw.GraphicsDevice _device;
        private readonly Rect2 _area;
        private readonly uint _width, _height;
        private readonly Mw.Texture _target;
        private readonly Handle _shown, _item;
        private readonly Mw.Buffer _units, _hits, _cells, _sorted, _world, _game, _lists, _args;
        private readonly Mw.ComputePipeline _initGame, _initWorld, _initUnits, _frameBegin, _seedFields, _relax,
            _clearCells, _countPass, _scan, _scatter, _strategy, _think, _resolve, _cull;
        private readonly Mw.Shader[] _shaders;
        private readonly Mw.GraphicsPipeline _ground, _units_, _tracers, _effects, _overlay;
        private Frame _frame;
        private int _wheel;
        private float _budget;

        public Stage(RtsLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _device = context.Device;
            _area = area;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            _target = Mw.Texture.Create2D(_device, "rts", _width, _height, Format,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

            const Mw.BufferUsageFlags state = Mw.BufferUsageFlags.ComputeStorageRead | Mw.BufferUsageFlags.ComputeStorageWrite;
            const Mw.BufferUsageFlags drawn = state | Mw.BufferUsageFlags.GraphicsStorageRead;
            _units = Words("rts units", drawn, MaxUnits * 64);
            _hits = Words("rts hits", state, MaxUnits * 8);
            _cells = Words("rts cells", drawn, Cells * 48);
            _sorted = Words("rts sorted", state, MaxUnits * 4);
            _world = Words("rts world", drawn, (uint)(Cells * (1 + Fields)) * 4);
            _game = Words("rts game", drawn, GameBytes);
            _lists = Words("rts lists", drawn, (2 * MaxUnits + Fx) * 4);
            _args = Words("rts args", drawn | Mw.BufferUsageFlags.Indirect, 16 * 4);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "rts_lab.slang");
            _initGame = Compute(module, "initGame");
            _initWorld = Compute(module, "initWorld");
            _initUnits = Compute(module, "initUnits");
            _frameBegin = Compute(module, "frameBegin");
            _seedFields = Compute(module, "seedFields");
            _relax = Compute(module, "relax");
            _clearCells = Compute(module, "clearCells");
            _countPass = Compute(module, "count");
            _scan = Compute(module, "scan", threads: 256);
            _scatter = Compute(module, "scatter");
            _strategy = Compute(module, "strategy");
            // Dispatched indirectly from the args buffer, so that buffer can't also be bound for writing.
            _think = Compute(module, "think", writes: 7);
            _resolve = Compute(module, "resolve");
            _cull = Compute(module, "cull");

            Mw.Shader full = Shader(module, "full", Mw.ShaderStage.Vertex);
            Mw.Shader ground = Shader(module, "ground", Mw.ShaderStage.Fragment, storage: 3, uniforms: 1);
            Mw.Shader overlay = Shader(module, "overlay", Mw.ShaderStage.Fragment, storage: 3, uniforms: 1);
            Mw.Shader unitVertex = Shader(module, "unitVertex", Mw.ShaderStage.Vertex, storage: 3, uniforms: 1);
            Mw.Shader unitShade = Shader(module, "unitShade", Mw.ShaderStage.Fragment);
            Mw.Shader tracerVertex = Shader(module, "tracerVertex", Mw.ShaderStage.Vertex, storage: 3, uniforms: 1);
            Mw.Shader tracerShade = Shader(module, "tracerShade", Mw.ShaderStage.Fragment);
            Mw.Shader effectVertex = Shader(module, "effectVertex", Mw.ShaderStage.Vertex, storage: 3, uniforms: 1);
            Mw.Shader effectShade = Shader(module, "effectShade", Mw.ShaderStage.Fragment);
            _shaders = [full, ground, overlay, unitVertex, unitShade, tracerVertex, tracerShade, effectVertex, effectShade];

            var opaque = new Mw.ColorTargetBlendState { EnableColorWriteMask = true, ColorWriteMask = Mw.ColorComponentFlags.RGBA };
            Mw.ColorTargetBlendState over = Blend(Mw.BlendFactor.One, Mw.BlendFactor.OneMinusSrcAlpha);
            Mw.ColorTargetBlendState add = Blend(Mw.BlendFactor.One, Mw.BlendFactor.One);
            _ground = Pipeline(full, ground, opaque);
            _effects = Pipeline(effectVertex, effectShade, add);
            _units_ = Pipeline(unitVertex, unitShade, over);
            _tracers = Pipeline(tracerVertex, tracerShade, add);
            _overlay = Pipeline(full, overlay, over);
        }

        private Mw.Buffer Words(string name, Mw.BufferUsageFlags usage, uint bytes) =>
            Mw.Buffer.Create<uint>(_device, name, usage, bytes / 4);

        private Mw.ComputePipeline Compute(string module, string entry, uint writes = 8, uint threads = Group) =>
            Mw.ComputePipeline.Create(_device, CachedSpirv(module, entry, "compute"), "main",
                new Mw.ComputePipelineCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    NumReadWriteStorageBuffers = writes,
                    NumUniformBuffers = 1,
                    ThreadCountX = threads,
                    ThreadCountY = 1,
                    ThreadCountZ = 1,
                });

        private Mw.Shader Shader(string module, string entry, Mw.ShaderStage stage, uint storage = 0, uint uniforms = 0) =>
            Mw.Shader.Create(_device, CachedSpirv(module, entry, stage == Mw.ShaderStage.Vertex ? "vertex" : "fragment"),
                "main", new Mw.ShaderCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    Stage = stage,
                    NumStorageBuffers = storage,
                    NumUniformBuffers = uniforms,
                });

        /// <summary>
        /// SPIR-V for one entry, kept in the shader cache by a hash of the module
        /// and the entry: the module has twenty-odd entries, and compiling them all
        /// fresh would stall the tab for seconds each time it opens.
        /// </summary>
        private static byte[] CachedSpirv(string module, string entry, string stage)
        {
            string source = File.ReadAllText(module);
            if (SlangShaders.CacheFolder is not { } folder)
                return SlangShaders.Compile(module, entry, stage);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"rts|{entry}|{stage}\n{source}"));
            string path = Path.Combine(folder, "rts-" + Convert.ToHexString(hash)[..32] + ".spv");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            byte[] spirv = SlangShaders.Compile(module, entry, stage);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, spirv);
            return spirv;
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
            _lab._ui.PollSteppers();
            _lab._ui.ShowSteppers();
            if (_lab._ui.Clicked(_lab._restartButton) || _lab._count != _lab._shownCount)
            {
                _lab._restart = true;
                _lab._shownCount = _lab._count;
            }

            uint buttons = 0, keys = 0;
            if (frame.Inputs is { } inputs)
            {
                Mouse mouse = inputs.Mouse;
                buttons = (mouse.LeftButton.IsDown ? 1u : 0) | (mouse.RightButton.IsDown ? 2u : 0) | (mouse.MiddleButton.IsDown ? 4u : 0);
                Vector2 local = frame.Mouse - _area.Position;
                if (local.X >= 0 && local.Y >= 0 && local.X < _width && local.Y < _height)
                    _wheel += mouse.Wheel;
                for (int bit = 0; bit < KeyBits.Length; bit++)
                    foreach (KeyCode code in KeyBits[bit])
                        if (inputs.Keyboard.IsDown(code))
                            keys |= 1u << bit;
            }

            // Battle time this frame, cut into steps no longer than MaxStep.
            _budget = Math.Min(frame.Delta, 0.1f) * LabPanel.Value(_lab._speed);
            int steps = Math.Min(MaxSteps, (int)MathF.Ceiling(_budget / MaxStep));
            Vector2 mouseAt = frame.Mouse - _area.Position;
            _frame = new Frame
            {
                Time = frame.Time,
                Dt = steps > 0 ? _budget / steps : 0,
                FrameDt = Math.Min(frame.Delta, 0.1f),
                Steps = steps,
                Width = _width,
                Height = _height,
                MouseX = mouseAt.X,
                MouseY = mouseAt.Y,
                Count = (uint)Counts[_lab._count],
                Buttons = buttons,
                Keys = keys,
                View = LabPanel.On(_lab._view) ? 1u : 0,
                Wheel = _wheel,
            };
        }

        private Mw.StorageBufferReadWriteBinding[] Bindings(bool withArgs = true) => withArgs
            ? [new(_units), new(_hits), new(_cells), new(_sorted), new(_world), new(_game), new(_lists), new(_args)]
            : [new(_units), new(_hits), new(_cells), new(_sorted), new(_world), new(_game), new(_lists)];

        /// <summary>One compute pass per dependent step: SDL_GPU orders storage writes between passes, not dispatches.</summary>
        private void Run(Mw.CommandBuffer commands, Mw.ComputePipeline pipeline, uint threads, uint group = Group)
        {
            Mw.ComputePass pass = commands.BeginComputePass([], Bindings());
            pass.BindComputePipeline(pipeline);
            commands.PushComputeUniformData(_frame);
            pass.Dispatch((threads + group - 1) / group, 1, 1);
            commands.EndComputePass(pass);
        }

        public void Render(Mw.CommandBuffer commands)
        {
            uint count = _frame.Count;
            if (_lab._restart)
            {
                Run(commands, _initGame, Fx);
                Run(commands, _initWorld, Cells);
                Run(commands, _initUnits, MaxUnits);
                _lab._restart = false;
            }

            Run(commands, _frameBegin, 1);
            _wheel = 0;
            Run(commands, _seedFields, (uint)(Cells * Fields));

            // Relaxing in place tolerates overlap, so every relaxation shares one pass.
            Mw.ComputePass relax = commands.BeginComputePass([], Bindings());
            relax.BindComputePipeline(_relax);
            commands.PushComputeUniformData(_frame);
            for (int i = 0; i < Relaxations; i++)
                relax.Dispatch((uint)(Cells * Fields + Group - 1) / Group, 1, 1);
            commands.EndComputePass(relax);

            for (int step = 0; step < (int)_frame.Steps; step++)
            {
                Run(commands, _clearCells, Cells);
                Run(commands, _countPass, count);
                Run(commands, _scan, 256, 256);
                Run(commands, _scatter, count);
                Run(commands, _strategy, 128);

                Mw.ComputePass think = commands.BeginComputePass([], Bindings(withArgs: false));
                think.BindComputePipeline(_think);
                commands.PushComputeUniformData(_frame);
                think.DispatchIndirect(_args, 12 * 4);
                commands.EndComputePass(think);

                Run(commands, _resolve, count);
            }
            Run(commands, _cull, Math.Max(count, Fx));

            Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, new Mw.Color(0.02f, 0.022f, 0.03f, 1f)));
            commands.PushVertexUniformData(_frame);
            commands.PushFragmentUniformData(_frame);

            pass.BindGraphicsPipeline(_ground);
            pass.BindFragmentStorageBuffers(_game, _cells, _world);
            pass.DrawPrimitives(3, 1, 0, 0);

            pass.BindGraphicsPipeline(_effects);
            pass.BindVertexStorageBuffers(_units, _lists, _game);
            pass.DrawPrimitivesIndirect(_args, 8 * 4, 1);

            pass.BindGraphicsPipeline(_units_);
            pass.BindVertexStorageBuffers(_units, _lists, _game);
            pass.DrawPrimitivesIndirect(_args, 0, 1);

            pass.BindGraphicsPipeline(_tracers);
            pass.BindVertexStorageBuffers(_units, _lists, _game);
            pass.DrawPrimitivesIndirect(_args, 4 * 4, 1);

            pass.BindGraphicsPipeline(_overlay);
            pass.BindFragmentStorageBuffers(_game, _cells, _world);
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            foreach (Mw.GraphicsPipeline pipeline in new[] { _ground, _units_, _tracers, _effects, _overlay })
                pipeline.Dispose();
            foreach (Mw.Shader shader in _shaders)
                shader.Dispose();
            foreach (Mw.ComputePipeline pipeline in new[] { _initGame, _initWorld, _initUnits, _frameBegin, _seedFields, _relax,
                         _clearCells, _countPass, _scan, _scatter, _strategy, _think, _resolve, _cull })
                pipeline.Dispose();
            foreach (Mw.Buffer buffer in new[] { _units, _hits, _cells, _sorted, _world, _game, _lists, _args })
                buffer.Dispose();
            _target.Dispose();
        }
    }
}
