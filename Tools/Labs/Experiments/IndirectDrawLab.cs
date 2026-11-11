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
/// Indirect drawing on its own. A field of up to a million drifting shapes
/// that exist only on the GPU (each made from a hash of its index), seen
/// through a camera. A compute pass culls them against a frame and packs the
/// survivors into a list with an atomic counter, and that counter is the
/// instance count of the draw command the render pass then reads. The CPU
/// sends the camera and the time, and never learns how many shapes were drawn;
/// the number on the stage is printed by the GPU from the same buffer.
/// </summary>
internal sealed class IndirectDrawLab : ILabExperiment
{
    private static readonly int[] Counts = [1_000, 10_000, 100_000, 250_000, 500_000, 1_000_000];
    private const int MaxCount = 1_000_000;
    private const uint Group = 64;

    private readonly LabPanel _ui = new();
    private int _count = 2;
    private Entity _indirect, _zoom, _inset, _size, _orbit, _readout;

    public string Name => "INDIRECT DRAW";

    public string[] Notes =>
    [
        "Shapes that exist only on the GPU, culled by a compute pass",
        "that also writes the draw's instance count (indirect). The",
        "number on the stage is the GPU's own count of what it drew.",
        "Pointer on the stage pans the camera; off it, it drifts.",
    ];

    public void BuildPanel(UiNode panel)
    {
        _ui.Stepper(panel, "SHAPES", () => _count, i => _count = i, Counts.Length, i => Counts[i].ToString("N0"));
        UiNode flags = LabPanel.Row(panel);
        _indirect = LabPanel.Toggle(flags, "INDIRECT", "DIRECT", true, 196,
            "INDIRECT: the GPU culls, packs and sizes the draw. DIRECT: every shape is drawn and the vertex shader throws away the culled ones");
        _zoom = LabPanel.Slider(panel, "ZOOM OUT", 0.25f, 8, 1, "World units per stage pixel: zoom out until everything is on screen", "0.00");
        _inset = LabPanel.Slider(panel, "CULL INSET", -200, 400, 120,
            "How far inside the stage's edges the cull frame sits, so you can watch shapes vanish at it (gold)", "0");
        _size = LabPanel.Slider(panel, "SIZE", 0.25f, 4, 1, "Every shape's size ×");
        _orbit = LabPanel.Slider(panel, "ORBIT", 0, 4, 1, "Every shape's orbit radius ×");
        _readout = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(LabPanel.Inner, LabPanel.RowHeight);

        panel.Text("Blending is additive: packing with atomics loses draw order,", UiType.Small, Palette.TextMuted);
        panel.Text("so anything order-dependent would flicker frame to frame.", UiType.Small, Palette.TextMuted);
        panel.Text("DIRECT draws every shape: the number is what it submitted.", UiType.Small, Palette.TextMuted);
    }

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, context, parent, area);

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms
    {
        public float Time, Count, Zoom, Mode;
        public float CameraX, CameraY, Width, Height;
        public float CullMinX, CullMinY, CullMaxX, CullMaxY;
        public float Size, Orbit, Pad0, Pad1;
    }

    private sealed class Stage : IToolStage
    {
        private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;
        private static readonly Vector2 World = new(12000, 8000);

        private readonly IndirectDrawLab _lab;
        private readonly LabContext _context;
        private readonly Mw.GraphicsDevice _device;
        private readonly Rect2 _area;
        private readonly uint _width, _height;
        private readonly Mw.Texture _target;
        private readonly Handle _shown, _item;
        private readonly Mw.Buffer _args, _visible;
        private readonly Mw.ComputePipeline _reset, _cull;
        private readonly Mw.Shader[] _shaders;
        private readonly Mw.GraphicsPipeline _shapes, _readout;
        private FrameUniforms _frame;
        private Vector2 _camera = World / 2;

        public Stage(IndirectDrawLab lab, LabContext context, Handle parent, Rect2 area)
        {
            _lab = lab;
            _context = context;
            _device = context.Device;
            _area = area;
            _width = (uint)area.Size.X;
            _height = (uint)area.Size.Y;

            _target = Mw.Texture.Create2D(_device, "indirect draw", _width, _height, Format,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

            // The draw command: written by compute, read by the draw and by the readout.
            _args = Mw.Buffer.Create<Mw.IndirectDrawCommand>(_device, "indirect draw command",
                Mw.BufferUsageFlags.Indirect | Mw.BufferUsageFlags.ComputeStorageWrite | Mw.BufferUsageFlags.GraphicsStorageRead, 1);
            _visible = Mw.Buffer.Create<uint>(_device, "indirect draw visible",
                Mw.BufferUsageFlags.ComputeStorageWrite | Mw.BufferUsageFlags.GraphicsStorageRead, MaxCount);

            string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "indirect_draw_lab.slang");
            _reset = Compute(module, "reset", writes: 1, uniforms: 0, threads: 1);
            _cull = Compute(module, "cull", writes: 2, uniforms: 1, threads: Group);

            Mw.Shader shapeVertex = Shader(module, "shapeVertex", Mw.ShaderStage.Vertex, storage: 1, uniforms: 1);
            Mw.Shader shapeShade = Shader(module, "shapeShade", Mw.ShaderStage.Fragment);
            Mw.Shader full = Shader(module, "full", Mw.ShaderStage.Vertex);
            Mw.Shader readout = Shader(module, "readout", Mw.ShaderStage.Fragment, storage: 1, uniforms: 1);
            _shaders = [shapeVertex, shapeShade, full, readout];

            _shapes = Pipeline(shapeVertex, shapeShade, Blend(Mw.BlendFactor.One, Mw.BlendFactor.One));
            _readout = Pipeline(full, readout, Blend(Mw.BlendFactor.One, Mw.BlendFactor.OneMinusSrcAlpha));
        }

        private Mw.ComputePipeline Compute(string module, string entry, uint writes, uint uniforms, uint threads) =>
            Mw.ComputePipeline.Create(_device, SlangShaders.Compile(module, entry, "compute"), "main",
                new Mw.ComputePipelineCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    NumReadWriteStorageBuffers = writes,
                    NumUniformBuffers = uniforms,
                    ThreadCountX = threads,
                    ThreadCountY = 1,
                    ThreadCountZ = 1,
                });

        private Mw.Shader Shader(string module, string entry, Mw.ShaderStage stage, uint storage = 0, uint uniforms = 0) =>
            Mw.Shader.Create(_device, SlangShaders.Compile(module, entry, stage == Mw.ShaderStage.Vertex ? "vertex" : "fragment"),
                "main", new Mw.ShaderCreateInfo
                {
                    Format = Mw.ShaderFormat.SPIRV,
                    Stage = stage,
                    NumStorageBuffers = storage,
                    NumUniformBuffers = uniforms,
                });

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

            float zoom = LabPanel.Value(_lab._zoom);
            var view = new Vector2(_width, _height) * zoom;

            // Pointer on the stage: it picks where in the world to look. Off it: a slow drift.
            Vector2 local = frame.Mouse - _area.Position;
            Vector2 aim;
            if (local.X >= 0 && local.Y >= 0 && local.X < _width && local.Y < _height)
                aim = local / new Vector2(_width, _height);
            else
                aim = new Vector2(0.5f + 0.4f * MathF.Sin(frame.Time * 0.13f), 0.5f + 0.4f * MathF.Sin(frame.Time * 0.21f));
            Vector2 goal = view / 2 + aim * (World - view);
            if (view.X >= World.X)
                goal.X = World.X / 2;
            if (view.Y >= World.Y)
                goal.Y = World.Y / 2;
            _camera = _camera.Lerp(goal, 1 - MathF.Exp(-4 * frame.Delta));

            float inset = LabPanel.Value(_lab._inset);
            int count = Counts[_lab._count];
            bool indirect = LabPanel.On(_lab._indirect);
            _frame = new FrameUniforms
            {
                Time = frame.Time, Count = count, Zoom = zoom, Mode = indirect ? 1 : 0,
                CameraX = _camera.X, CameraY = _camera.Y, Width = _width, Height = _height,
                CullMinX = inset, CullMinY = inset, CullMaxX = _width - inset, CullMaxY = _height - inset,
                Size = LabPanel.Value(_lab._size), Orbit = LabPanel.Value(_lab._orbit),
            };

            int bytes = Marshal.SizeOf<FrameUniforms>();
            UiText.Set(_lab._readout, indirect
                ? $"CPU → GPU: {bytes} B of uniforms · 1 indirect draw, count unknown to the CPU"
                : $"CPU → GPU: {bytes} B of uniforms · 1 draw of {count:N0} instances");
        }

        public void Render(Mw.CommandBuffer commands)
        {
            uint count = (uint)_frame.Count;
            bool indirect = _frame.Mode > 0.5f;

            if (indirect)
            {
                // Two passes: SDL_GPU syncs storage writes between compute passes, not between dispatches in one.
                Mw.ComputePass reset = commands.BeginComputePass(new Mw.StorageBufferReadWriteBinding(_args));
                reset.BindComputePipeline(_reset);
                reset.Dispatch(1, 1, 1);
                commands.EndComputePass(reset);

                Mw.ComputePass cull = commands.BeginComputePass([],
                    [new Mw.StorageBufferReadWriteBinding(_args), new Mw.StorageBufferReadWriteBinding(_visible)]);
                cull.BindComputePipeline(_cull);
                commands.PushComputeUniformData(_frame);
                cull.Dispatch((count + Group - 1) / Group, 1, 1);
                commands.EndComputePass(cull);
            }

            Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target, new Mw.Color(0.02f, 0.025f, 0.045f, 1f)));
            commands.PushVertexUniformData(_frame);
            commands.PushFragmentUniformData(_frame);

            pass.BindGraphicsPipeline(_shapes);
            pass.BindVertexStorageBuffers(_visible);
            if (indirect)
                pass.DrawPrimitivesIndirect(_args, 0, 1);
            else
                pass.DrawPrimitives(6, count, 0, 0);

            pass.BindGraphicsPipeline(_readout);
            pass.BindFragmentStorageBuffers(_args);
            pass.DrawPrimitives(3, 1, 0, 0);
            commands.EndRenderPass(pass);
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            _context.Hide(_shown);
            _shapes.Dispose();
            _readout.Dispose();
            foreach (Mw.Shader shader in _shaders)
                shader.Dispose();
            _reset.Dispose();
            _cull.Dispose();
            _args.Dispose();
            _visible.Dispose();
            _target.Dispose();
        }
    }
}
