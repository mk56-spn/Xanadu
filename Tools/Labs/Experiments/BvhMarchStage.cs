using MoonWorks.Graphics;
using Xanadu.Host.Rendering;
using Xanadu.Host.Rendering.Compute;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// <see cref="BvhMarchLab"/>'s GPU side: the buffers, the passes and their
/// order. Each dependent compute step is a pass of its own, since SDL_GPU
/// orders storage writes between passes, not between dispatches.
/// </summary>
internal sealed class BvhMarchStage : IToolStage
{
    private const uint SortGroup = 256, Digits = 256, Tile = 8, TileCap = 256;
    private const uint Pillars = 10, Sparks = 6144;
    private const uint LightCells = 256 * 256, LightEntries = 8u << 20;
    private const Mw.TextureFormat RasterFormat = Mw.TextureFormat.R16G16B16A16Float;
    private const Mw.TextureFormat OutFormat = Mw.TextureFormat.R8G8B8A8Unorm;
    private const Mw.TextureFormat DepthFormat = Mw.TextureFormat.D32Float;
    private static readonly Vector3 Target = new(0, 3.6f, 0);

    private readonly BvhMarchLab _lab;
    private readonly LabContext _context;
    private readonly Mw.GraphicsDevice _device;
    private readonly Rect2 _area;
    private readonly uint _width, _height, _tilesX, _tilesY;
    private readonly Texture _raster, _depth, _out, _outDepth;
    private readonly Sampler _point;
    private readonly Buffer[] _buffers;
    private readonly ComputePipeline _animate, _hist, _scan, _scatter, _build, _refit, _tiles, _march, _lightCount, _lightScan, _lightFill;
    private readonly Shader[] _shaders;
    private readonly GraphicsPipeline _floor, _pillars, _writeDepth, _sparks;
    private readonly Handle _shown, _item;
    private readonly GpuTimes.FrameHandler _timed;
    private BvhMarchFrame _frame;
    private float _time, _yaw = 0.6f, _pitch = 0.42f, _distance = 21;
    private Vector2 _lastMouse;
    private bool _dragging;

    public BvhMarchStage(BvhMarchLab lab, LabContext context, Handle parent, Rect2 area)
    {
        _lab = lab;
        _context = context;
        _device = context.Device;
        _area = area;
        _width = (uint)area.Size.X;
        _height = (uint)area.Size.Y;
        _tilesX = (_width + Tile - 1) / Tile;
        _tilesY = (_height + Tile - 1) / Tile;

        const Mw.TextureUsageFlags sampled = Mw.TextureUsageFlags.Sampler;
        _raster = Texture.Create2D(_device, "bvh raster", _width, _height, RasterFormat, Mw.TextureUsageFlags.ColorTarget | sampled);
        _depth = Texture.Create2D(_device, "bvh depth", _width, _height, DepthFormat, Mw.TextureUsageFlags.DepthStencilTarget | sampled);
        _out = Texture.Create2D(_device, "bvh out", _width, _height, OutFormat,
            Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.ComputeStorageWrite | sampled);
        _outDepth = Texture.Create2D(_device, "bvh out depth", _width, _height, Mw.TextureFormat.R32Float,
            Mw.TextureUsageFlags.ComputeStorageWrite | sampled);
        _point = Sampler.Create(_device, Mw.SamplerCreateInfo.PointClamp);

        uint n = BvhMarchLab.MaxCount;
        uint tiles = _tilesX * _tilesY;
        // Bindings 2..9 of the shader, packed to SDL_GPU's 8 read-write buffers a pipeline.
        _buffers =
        [
            Words("bvh balls", 2 * n * 4),
            Words("bvh keys", 2 * n),
            Words("bvh vals", 2 * n),
            Words("bvh links", Digits * (n / SortGroup) + 2 * n),
            Words("bvh nodes", 2 * n * 8),
            Words("bvh arrivals", n),
            Words("bvh tiles", tiles * (2 + TileCap)),
            Words("bvh light", 2 * LightCells + LightEntries),
        ];

        string module = LabShaders.Module("bvh_march_lab.slang");
        _animate = Compute(module, "animate", 256);
        _hist = Compute(module, "hist", 256);
        _scan = Compute(module, "scan", 1024);
        _scatter = Compute(module, "scatter", 256);
        _build = Compute(module, "build", 256);
        _refit = Compute(module, "refit", 256);
        _tiles = Compute(module, "tiles", 8, 8);
        _march = Compute(module, "march", 8, 8);
        _lightCount = Compute(module, "lightCount", 256);
        _lightScan = Compute(module, "lightScan", 1024);
        _lightFill = Compute(module, "lightFill", 256);

        Shader floorVertex = Shader(module, "floorVertex", Mw.ShaderStage.Vertex, uniforms: 1);
        Shader pillarVertex = Shader(module, "pillarVertex", Mw.ShaderStage.Vertex, uniforms: 1);
        Shader sparkVertex = Shader(module, "sparkVertex", Mw.ShaderStage.Vertex, uniforms: 1);
        Shader fullVertex = Shader(module, "fullVertex", Mw.ShaderStage.Vertex);
        Shader lit = Shader(module, "litFragment", Mw.ShaderStage.Fragment, uniforms: 1);
        Shader spark = Shader(module, "sparkFragment", Mw.ShaderStage.Fragment);
        Shader writeDepth = Shader(module, "writeDepth", Mw.ShaderStage.Fragment, samplers: 1, uniforms: 1);
        _shaders = [floorVertex, pillarVertex, sparkVertex, fullVertex, lit, spark, writeDepth];

        var opaque = new Mw.ColorTargetBlendState { EnableColorWriteMask = true, ColorWriteMask = Mw.ColorComponentFlags.RGBA };
        var none = new Mw.ColorTargetBlendState { EnableColorWriteMask = true, ColorWriteMask = Mw.ColorComponentFlags.None };
        var add = new Mw.ColorTargetBlendState
        {
            EnableBlend = true,
            SrcColorBlendFactor = Mw.BlendFactor.One, DstColorBlendFactor = Mw.BlendFactor.One, ColorBlendOp = Mw.BlendOp.Add,
            SrcAlphaBlendFactor = Mw.BlendFactor.Zero, DstAlphaBlendFactor = Mw.BlendFactor.One, AlphaBlendOp = Mw.BlendOp.Add,
            EnableColorWriteMask = true, ColorWriteMask = Mw.ColorComponentFlags.RGBA,
        };
        // Reverse z: nearer is greater, cleared to 0.
        _floor = Pipeline(floorVertex, lit, RasterFormat, opaque, Mw.CompareOp.Greater, write: true);
        _pillars = Pipeline(pillarVertex, lit, RasterFormat, opaque, Mw.CompareOp.Greater, write: true);
        _writeDepth = Pipeline(fullVertex, writeDepth, OutFormat, none, Mw.CompareOp.Always, write: true);
        _sparks = Pipeline(sparkVertex, spark, OutFormat, add, Mw.CompareOp.Greater, write: false);

        _item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_item, parent);
        _shown = context.Show(_out);
        CanvasServer.Current.AddTextureRect(_item, new Rect2(area.Position, new Vector2(_width, _height)), _shown);

        _timed = lab.Timed;
        GpuTimes.FrameTimed += _timed;
    }

    private Buffer Words(string name, uint words) => Buffer.Create<uint>(_device, name,
        Mw.BufferUsageFlags.ComputeStorageRead | Mw.BufferUsageFlags.ComputeStorageWrite, words);

    private ComputePipeline Compute(string module, string entry, uint x, uint y = 1) =>
        ComputePipelines.Create(_device, LabShaders.Cached(module, entry, "compute"), entry,
            new Mw.ComputePipelineCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                NumSamplers = 2,
                NumReadWriteStorageTextures = 2,
                NumReadWriteStorageBuffers = (uint)_buffers.Length,
                NumUniformBuffers = 1,
                ThreadCountX = x,
                ThreadCountY = y,
                ThreadCountZ = 1,
            });

    private Shader Shader(string module, string entry, Mw.ShaderStage stage, uint samplers = 0, uint uniforms = 0) =>
        MoonWorks.Graphics.Shader.Create(_device, LabShaders.Cached(module, entry, stage == Mw.ShaderStage.Vertex ? "vertex" : "fragment"),
            "main", new Mw.ShaderCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                Stage = stage,
                NumSamplers = samplers,
                NumUniformBuffers = uniforms,
            });

    private GraphicsPipeline Pipeline(Shader vertex, Shader fragment, Mw.TextureFormat format, Mw.ColorTargetBlendState blend,
        Mw.CompareOp compare, bool write) =>
        GraphicsPipeline.Create(_device, new Mw.GraphicsPipelineCreateInfo
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            VertexInputState = Mw.VertexInputState.Empty,
            PrimitiveType = Mw.PrimitiveType.TriangleList,
            RasterizerState = Mw.RasterizerState.CCW_CullNone,
            MultisampleState = Mw.MultisampleState.None,
            DepthStencilState = new Mw.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = write,
                CompareOp = compare,
            },
            TargetInfo = new Mw.GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = format, BlendState = blend }],
                DepthStencilFormat = DepthFormat,
                HasDepthStencilTarget = true,
            },
        });

    public void Update(in ToolFrame frame)
    {
        _lab.Poll();
        _lab.ShowReadout(_width, _height);
        _time += Math.Min(frame.Delta, 0.1f) * _lab.Speed;

        // Drag to orbit, wheel to zoom; the orbit toggle turns it slowly otherwise.
        bool inside = _area.HasPoint(frame.Mouse);
        if (frame.Held && !_dragging && inside)
            _dragging = true;
        if (!frame.Held)
            _dragging = false;
        if (_dragging)
        {
            Vector2 moved = frame.Mouse - _lastMouse;
            _yaw -= moved.X * 0.006f;
            _pitch = Math.Clamp(_pitch + moved.Y * 0.004f, 0.05f, 1.45f);
        }
        _lastMouse = frame.Mouse;
        if (inside)
            _distance = Math.Clamp(_distance * MathF.Pow(0.9f, frame.Wheel), 4, 60);
        if (_lab.Orbit && !_dragging)
            _yaw += Math.Min(frame.Delta, 0.1f) * 0.08f;

        Vector3 eye = Target + _distance * new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        Vector3 forward = Vector3.Normalize(Target - eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        float focalY = 1 / MathF.Tan(55 * MathF.PI / 360);
        float focalX = focalY * _height / _width;

        int count = _lab.Count;
        float radius = 0.32f * MathF.Cbrt(4096f / count);
        uint flags = (_lab.Shadows > 0 ? 1u : 0) | (_lab.Tiles ? 2u : 0) | (_lab.Shadows == 2 ? 4u : 0);
        Vector3 sun = Vector3.Normalize(new Vector3(-0.45f, 0.8f, 0.35f));
        _frame = new BvhMarchFrame
        {
            Eye = new Vector4(eye, _time),
            Right = new Vector4(right, focalX),
            Up = new Vector4(up, focalY),
            Forward = new Vector4(forward, 0.05f),
            Size = new Vector4(_width, _height, _tilesX, _tilesY),
            Count = (uint)count,
            Blocks = (uint)count / SortGroup,
            View = _lab.View,
            Look = new Vector4(_lab.Blend * radius, radius, _lab.Softness, 1),
            Sun = new Vector4(sun, BitConverter.UInt32BitsToSingle(flags)),
        };
    }

    /// <summary>One compute pass: everything bound, the frame pushed, one dispatch.</summary>
    private void Run(Mw.CommandBuffer commands, ComputePipeline pipeline, uint x, uint y = 1)
    {
        Span<Mw.StorageBufferReadWriteBinding> buffers = stackalloc Mw.StorageBufferReadWriteBinding[_buffers.Length];
        for (int i = 0; i < _buffers.Length; i++)
            buffers[i] = new Mw.StorageBufferReadWriteBinding(_buffers[i]);
        Mw.ComputePass pass = commands.BeginComputePass([new(_out), new(_outDepth)], buffers);
        pass.BindComputePipeline(pipeline);
        pass.BindSamplers(new Mw.TextureSamplerBinding(_raster, _point), new Mw.TextureSamplerBinding(_depth, _point));
        commands.PushComputeUniformData(_frame);
        pass.Dispatch(x, y, 1);
        commands.EndComputePass(pass);
    }

    /// <summary>The depth buffer, kept: the march samples it, the sparks test against it. Reverse z clears to 0.</summary>
    private Mw.DepthStencilTargetInfo DepthTarget(Mw.LoadOp load) => new()
    {
        Texture = _depth.Handle,
        LoadOp = load,
        StoreOp = Mw.StoreOp.Store,
        ClearDepth = 0,
        StencilLoadOp = Mw.LoadOp.DontCare,
        StencilStoreOp = Mw.StoreOp.DontCare,
    };

    public void Render(Mw.CommandBuffer commands)
    {
        uint n = _frame.Count, blocks = _frame.Blocks;
        uint groups = (n + 255) / 256;

        // Animate also zeroes the light grid's counts, a thread a cell.
        using (commands.Scope("bvh animate"))
            Run(commands, _animate, Math.Max(groups, LightCells / 256));

        // Four 8-bit digits of the Morton codes; A → B → A → B → A, so the sorted codes land in A.
        using (commands.Scope("bvh sort"))
        {
            for (uint pass = 0; pass < 4; pass++)
            {
                _frame.Pass = pass;
                Run(commands, _hist, blocks);
                Run(commands, _scan, 1);
                Run(commands, _scatter, blocks);
            }
        }

        using (commands.Scope("bvh build"))
            Run(commands, _build, groups);
        using (commands.Scope("bvh refit"))
            Run(commands, _refit, groups);

        if (_lab.Shadows == 2)
        {
            using (commands.Scope("bvh light"))
            {
                Run(commands, _lightCount, groups);
                Run(commands, _lightScan, 1);
                Run(commands, _lightFill, groups);
            }
        }

        using (commands.Scope("bvh raster"))
        {
            Mw.RenderPass pass = commands.BeginRenderPass(
                DepthTarget(Mw.LoadOp.Clear),
                new Mw.ColorTargetInfo(_raster, new Mw.Color(0, 0, 0, 0)));
            commands.PushVertexUniformData(_frame);
            commands.PushFragmentUniformData(_frame);
            pass.BindGraphicsPipeline(_floor);
            pass.DrawPrimitives(6, 1, 0, 0);
            pass.BindGraphicsPipeline(_pillars);
            pass.DrawPrimitives(36, Pillars, 0, 0);
            commands.EndRenderPass(pass);
        }

        if (_lab.Tiles || _lab.View == 3)
        {
            using (commands.Scope("bvh tiles"))
                Run(commands, _tiles, (_tilesX + 7) / 8, (_tilesY + 7) / 8);
        }
        using (commands.Scope("bvh march"))
            Run(commands, _march, _tilesX, _tilesY);

        // The march's depth into the depth buffer, then sparks tested against blobs and raster alike.
        using (commands.Scope("bvh sparks"))
        {
            Mw.RenderPass pass = commands.BeginRenderPass(
                DepthTarget(Mw.LoadOp.Load),
                new Mw.ColorTargetInfo(_out, Mw.LoadOp.Load));
            commands.PushVertexUniformData(_frame);
            commands.PushFragmentUniformData(_frame);
            if (_lab.Sparks)
            {
                pass.BindGraphicsPipeline(_writeDepth);
                pass.BindFragmentSamplers(new Mw.TextureSamplerBinding(_outDepth, _point));
                pass.DrawPrimitives(3, 1, 0, 0);
                pass.BindGraphicsPipeline(_sparks);
                pass.DrawPrimitives(6, Sparks, 0, 0);
            }
            commands.EndRenderPass(pass);
        }
    }

    public void Dispose()
    {
        GpuTimes.FrameTimed -= _timed;
        CanvasServer.Current.Free(_item);
        _context.Hide(_shown);
        foreach (GraphicsPipeline pipeline in (GraphicsPipeline[])[_floor, _pillars, _writeDepth, _sparks])
            pipeline.Dispose();
        foreach (Shader shader in _shaders)
            shader.Dispose();
        foreach (ComputePipeline pipeline in (ComputePipeline[])[_animate, _hist, _scan, _scatter, _build, _refit, _tiles, _march, _lightCount, _lightScan, _lightFill])
            pipeline.Dispose();
        foreach (Buffer buffer in _buffers)
            buffer.Dispose();
        foreach (Texture texture in (Texture[])[_raster, _depth, _out, _outDepth])
            texture.Dispose();
        _point.Dispose();
    }
}
