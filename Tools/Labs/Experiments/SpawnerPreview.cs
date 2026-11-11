using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Labs.Editor;
using Xanadu.Systems.Canvas;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Indirect drawing made easy, as the shader editor's preview. The code says
/// how many things there are (<c>count</c>) and what thing number i is
/// (<c>Thing spawn(uint i)</c>), and optionally how one looks
/// (<c>shade(Thing t, Canvas canvas)</c>). It is wrapped into a compute pass
/// that spawns, culls and packs the things, and one indirect draw of the
/// survivors. <c>static const bool additive = true;</c> blends by adding.
/// </summary>
internal sealed class SpawnerPreview(LabContext context, Handle item) : IEditorPreview
{
    /// <summary>The most things the code may spawn; <c>count</c> past it is cut to it.</summary>
    private const uint MaxCount = 1 << 20;

    private const uint Group = 64;
    private const Mw.TextureFormat Format = Mw.TextureFormat.R8G8B8A8Unorm;

    /// <summary>What the code sees besides the library: <c>Thing</c>. Public, so completion reads its fields and docs.</summary>
    private const string ThingSource = """
        /// One thing the spawner draws: spawn(i) fills one in. Every field starts at its default.
        public struct Thing
        {
            /// Pixels from the stage's centre, y down. Starts at (0, 0).
            public float2 position;
            /// Full width and height in pixels. Starts at (16, 16).
            public float2 size;
            /// Radians, clockwise. Starts at 0.
            public float rotation;
            /// Straight alpha. Starts white.
            public float4 colour;
            /// True skips it. Starts false.
            public bool hidden;
            /// Which one it is: the i spawn was given.
            public uint index;

            // Defaults here, not on the fields: Slang leaves a plain `Thing t;` undefined otherwise.
            __init()
            {
                position = float2(0, 0);
                size = float2(16, 16);
                rotation = 0;
                colour = float4(1, 1, 1, 1);
                hidden = false;
                index = 0;
            }
        };
        """;

    private readonly Mw.GraphicsDevice _device = context.Device;
    private readonly Mw.Buffer _args = Mw.Buffer.Create<Mw.IndirectDrawCommand>(context.Device, "spawner draw command",
        Mw.BufferUsageFlags.Indirect | Mw.BufferUsageFlags.ComputeStorageWrite, 1);
    private readonly Mw.Buffer _things = Mw.Buffer.Create<Stored>(context.Device, "spawner things",
        Mw.BufferUsageFlags.ComputeStorageWrite | Mw.BufferUsageFlags.GraphicsStorageRead, MaxCount);

    private Mw.Texture? _target;
    private Handle _shown;
    private Programs? _programs;
    private SpawnFrame _frame;
    private Rect2 _area;

    public string Label => "spawner";

    public string[] Help =>
    [
        "static const uint count = …; Thing spawn(uint i) says what thing i is; optional float4 shade(Thing t, Canvas canvas).",
        "Use time, dt, mouse or resolution and the lab fills it; no need to declare them.",
    ];

    public IEnumerable<(string Name, string Type, string? Note)> Provided =>
    [
        ("time", "float", "Seconds since the lab started."),
        ("dt", "float", "This frame's length in seconds."),
        ("mouse", "float2", "The pointer, in pixels from the preview's centre (like a thing's position)."),
        ("resolution", "float2", "The preview's size in pixels."),
        ("count", "uint", "How many things to spawn, up to about a million: static const uint count = 1000;"),
    ];

    public string Prelude => ThingSource;

    public IEnumerable<string> Types => ["Thing"];

    // ---- compiling -----------------------------------------------------------------

    /// <summary>One SPIR-V per entry, and how to blend.</summary>
    private sealed record Compiled(byte[] Reset, byte[] Spawn, byte[] Vertex, byte[] Fragment, bool Additive);

    public PreviewBuild Compile(string source)
    {
        string file = Path.Combine(Path.GetTempPath(), $"xanadu-spawner-{Environment.ProcessId}-{Guid.NewGuid():N}.slang");
        File.WriteAllText(file, Wrap(source, out bool additive));
        try
        {
            var compiled = new Compiled(
                SlangShaders.Compile(file, "xanaduReset", "compute"),
                SlangShaders.Compile(file, "xanaduSpawn", "compute"),
                SlangShaders.Compile(file, "xanaduVertex", "vertex"),
                SlangShaders.Compile(file, "xanaduFragment", "fragment"),
                additive);
            return new PreviewBuild(compiled, []);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The module around the code: the provided values and <c>Thing</c> before
    /// it, and after it the compute entries (reset, spawn) and the draw's vertex
    /// and fragment entries. <c>#line</c> keeps errors on the code's own lines.
    /// </summary>
    private string Wrap(string source, out bool additive)
    {
        (string imports, string code, string label) = SlangShaders.Split(Label, source);
        string stripped = Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", "");
        // Problems the code can't compile without, worded as Slang's so the editor lists them.
        if (!Regex.IsMatch(stripped, @"\bcount\s*="))
            throw new InvalidOperationException($"{Label}(1): error: say how many things there are: static const uint count = 1000;");
        if (!Regex.IsMatch(stripped, @"\bspawn\s*\("))
            throw new InvalidOperationException($"{Label}(1): error: write Thing spawn(uint i), which says what thing number i is.");
        additive = Regex.IsMatch(stripped, @"\badditive\s*=\s*true\b");

        string shade = Regex.IsMatch(stripped, @"\bshade\s*\(") ? "" : """
            float4 shade(Thing t, Canvas canvas)
            {
                return t.colour.withAlpha(t.colour.a * Circle(min(t.size.x, t.size.y) / 2).filled());
            }
            """;

        return $$"""
            import xanadu;
            {{imports}}
            struct XanaduSpawnFrame { float time; float dt; float2 resolution; float2 mouse; float2 pad; };
            static float time;
            static float dt;
            static float2 resolution;
            static float2 mouse;
            void xanaduLoad(XanaduSpawnFrame f) { time = f.time; dt = f.dt; resolution = f.resolution; mouse = f.mouse; }

            {{ThingSource}}
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            {{shade}}

            struct XanaduStored { float4 placed; float4 colour; float rotation; uint index; float2 pad; };

            [[vk::binding(0, 1)]] RWStructuredBuffer<uint> xanaduArgs;
            [[vk::binding(1, 1)]] RWStructuredBuffer<XanaduStored> xanaduOut;
            [[vk::binding(0, 2)]] ConstantBuffer<XanaduSpawnFrame> xanaduComputeFrame;
            [[vk::binding(0, 0)]] StructuredBuffer<XanaduStored> xanaduThings;
            [[vk::binding(0, 1)]] ConstantBuffer<XanaduSpawnFrame> xanaduVertexFrame;
            [[vk::binding(0, 3)]] ConstantBuffer<XanaduSpawnFrame> xanaduFragmentFrame;

            static const float XanaduMargin = 2;

            [shader("compute")]
            [numthreads(1, 1, 1)]
            void xanaduReset()
            {
                xanaduArgs[0] = 6;
                xanaduArgs[1] = 0;
                xanaduArgs[2] = 0;
                xanaduArgs[3] = 0;
            }

            [shader("compute")]
            [numthreads({{Group}}, 1, 1)]
            void xanaduSpawn(uint3 id : SV_DispatchThreadID)
            {
                uint i = id.x;
                if (i >= min(uint(count), {{MaxCount}}u))
                    return;
                xanaduLoad(xanaduComputeFrame);
                Thing t = spawn(i);
                if (t.hidden || t.size.x <= 0 || t.size.y <= 0)
                    return;
                float reach = length(t.size) * 0.5 + XanaduMargin;
                if (any(abs(t.position) - reach > resolution * 0.5))
                    return;

                uint slot;
                InterlockedAdd(xanaduArgs[1], 1u, slot);
                XanaduStored s;
                s.placed = float4(t.position, t.size);
                s.colour = t.colour;
                s.rotation = t.rotation;
                s.index = i;
                s.pad = float2(0, 0);
                xanaduOut[slot] = s;
            }

            struct XanaduVaryings
            {
                float4 position : SV_Position;
                float2 uv : TEXCOORD0;
                float4 colour : COLOR0;
                float2 local : TEXCOORD1;
                nointerpolation float4 placed : TEXCOORD2;
                nointerpolation float rotation : TEXCOORD3;
                nointerpolation uint index : TEXCOORD4;
            };

            static const float2 XanaduCorners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

            [shader("vertex")]
            XanaduVaryings xanaduVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                xanaduLoad(xanaduVertexFrame);
                XanaduStored s = xanaduThings[instance];
                float2 size = s.placed.zw;
                // A margin round the thing, so shapes that fill it keep their soft edge.
                float2 local = XanaduCorners[vertex] * (size * 0.5 + XanaduMargin);
                float2 pixel = s.placed.xy + rotate(local, s.rotation) + resolution * 0.5;
                float2 ndc = pixel / resolution * 2 - 1;

                XanaduVaryings o;
                o.position = float4(ndc.x, -ndc.y, 0, 1);
                o.uv = local / size + 0.5;
                o.colour = s.colour;
                o.local = local;
                o.placed = s.placed;
                o.rotation = s.rotation;
                o.index = s.index;
                return o;
            }

            [shader("fragment")]
            float4 xanaduFragment(XanaduVaryings v) : SV_Target
            {
                xanaduLoad(xanaduFragmentFrame);
                ItemVaryings item;
                item.position = v.position;
                item.uv = v.uv;
                item.colour = v.colour;
                item.local = v.local;

                ItemInput i;
                i.uv = v.uv;
                i.colour = v.colour;
                i.vertexColour = v.colour;
                i.local = v.local;
                i.pixel = v.position.xy;
                i.screenUv = v.position.xy / resolution;
                i.time = time;
                i.canvas = canvasOf(item);
                shading = i;

                Thing t;
                t.position = v.placed.xy;
                t.size = v.placed.zw;
                t.rotation = v.rotation;
                t.colour = v.colour;
                t.index = v.index;
                float4 c = shade(t, i.canvas);
                return float4(c.rgb * c.a, c.a);
            }
            """;
    }

    // ---- showing -------------------------------------------------------------------

    public string Show(PreviewBuild build)
    {
        var code = (Compiled)build.Result;
        Mw.ComputePipeline reset = Compute(code.Reset, writes: 1, uniforms: 0, threads: 1);
        Mw.ComputePipeline spawn = Compute(code.Spawn, writes: 2, uniforms: 1, threads: Group);
        Mw.Shader vertex = Mw.Shader.Create(_device, code.Vertex, "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV, Stage = Mw.ShaderStage.Vertex, NumStorageBuffers = 1, NumUniformBuffers = 1,
        });
        Mw.Shader fragment = Mw.Shader.Create(_device, code.Fragment, "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV, Stage = Mw.ShaderStage.Fragment, NumUniformBuffers = 1,
        });
        Mw.GraphicsPipeline draw = Pipeline(vertex, fragment, code.Additive ? Mw.BlendFactor.One : Mw.BlendFactor.OneMinusSrcAlpha);

        _programs?.Dispose();
        _programs = new Programs(reset, spawn, draw, vertex, fragment);
        return code.Additive ? " · additive" : "";
    }

    /// <summary>One compiled version's GPU objects, swapped whole when the code compiles again.</summary>
    private sealed class Programs(Mw.ComputePipeline reset, Mw.ComputePipeline spawn, Mw.GraphicsPipeline draw, Mw.Shader vertex, Mw.Shader fragment) : IDisposable
    {
        public Mw.ComputePipeline Reset { get; } = reset;
        public Mw.ComputePipeline Spawn { get; } = spawn;
        public Mw.GraphicsPipeline Draw { get; } = draw;

        public void Dispose()
        {
            Reset.Dispose();
            Spawn.Dispose();
            Draw.Dispose();
            vertex.Dispose();
            fragment.Dispose();
        }
    }

    /// <summary>The wrapper's <c>XanaduStored</c>, for sizing the buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Stored
    {
        public Vector4 Placed, Colour;
        public float Rotation;
        public uint Index;
        public Vector2 Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpawnFrame
    {
        public float Time, Dt, Width, Height;
        public float MouseX, MouseY, Pad0, Pad1;
    }

    private Mw.ComputePipeline Compute(byte[] spirv, uint writes, uint uniforms, uint threads) =>
        Mw.ComputePipeline.Create(_device, spirv, "main", new Mw.ComputePipelineCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV,
            NumReadWriteStorageBuffers = writes,
            NumUniformBuffers = uniforms,
            ThreadCountX = threads,
            ThreadCountY = 1,
            ThreadCountZ = 1,
        });

    /// <summary>Premultiplied colour on: <paramref name="destination"/> OneMinusSrcAlpha is over, One is add.</summary>
    private Mw.GraphicsPipeline Pipeline(Mw.Shader vertex, Mw.Shader fragment, Mw.BlendFactor destination) =>
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
                ColorTargetDescriptions =
                [
                    new Mw.ColorTargetDescription
                    {
                        Format = Format,
                        BlendState = new Mw.ColorTargetBlendState
                        {
                            EnableBlend = true,
                            SrcColorBlendFactor = Mw.BlendFactor.One,
                            DstColorBlendFactor = destination,
                            ColorBlendOp = Mw.BlendOp.Add,
                            SrcAlphaBlendFactor = Mw.BlendFactor.One,
                            DstAlphaBlendFactor = destination,
                            AlphaBlendOp = Mw.BlendOp.Add,
                            EnableColorWriteMask = true,
                            ColorWriteMask = Mw.ColorComponentFlags.RGBA,
                        },
                    },
                ],
            },
        });

    /// <summary>A target the preview's size, made again when the divider changes it.</summary>
    public void Place(Rect2 area)
    {
        _area = area;
        uint width = (uint)MathF.Max(1, area.Size.X), height = (uint)MathF.Max(1, area.Size.Y);
        if (_target is null || _target.Width != width || _target.Height != height)
        {
            if (_target is not null)
            {
                context.Hide(_shown);
                _target.Dispose();
            }

            _target = Mw.Texture.Create2D(_device, "spawner", width, height, Format,
                Mw.TextureUsageFlags.ColorTarget | Mw.TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
        }

        CanvasServer.Current.Clear(item);
        CanvasServer.Current.AddTextureRect(item, area, _shown);
    }

    public void Update(in LabFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        Vector2 mouse = pointer - _area.GetCenter();
        _frame = new SpawnFrame
        {
            Time = frame.Time, Dt = frame.Delta, Width = _target!.Width, Height = _target.Height,
            MouseX = mouse.X, MouseY = mouse.Y,
        };
    }

    public void Render(Mw.CommandBuffer commands)
    {
        var background = new Mw.Color(0.043f, 0.063f, 0.125f, 1f);
        if (_programs is not { } programs)
        {
            commands.EndRenderPass(commands.BeginRenderPass(new Mw.ColorTargetInfo(_target!, background)));
            return;
        }

        // Separate passes: SDL_GPU syncs storage writes between compute passes, not between dispatches in one.
        Mw.ComputePass reset = commands.BeginComputePass(new Mw.StorageBufferReadWriteBinding(_args));
        reset.BindComputePipeline(programs.Reset);
        reset.Dispatch(1, 1, 1);
        commands.EndComputePass(reset);

        // count lives in the shader, so every slot up to the cap gets a thread; past count they return at once.
        Mw.ComputePass spawn = commands.BeginComputePass([],
            [new Mw.StorageBufferReadWriteBinding(_args), new Mw.StorageBufferReadWriteBinding(_things)]);
        spawn.BindComputePipeline(programs.Spawn);
        commands.PushComputeUniformData(_frame);
        spawn.Dispatch(MaxCount / Group, 1, 1);
        commands.EndComputePass(spawn);

        Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(_target!, background));
        commands.PushVertexUniformData(_frame);
        commands.PushFragmentUniformData(_frame);
        pass.BindGraphicsPipeline(programs.Draw);
        pass.BindVertexStorageBuffers(_things);
        pass.DrawPrimitivesIndirect(_args, 0, 1);
        commands.EndRenderPass(pass);
    }

    public void Dispose()
    {
        _programs?.Dispose();
        if (_target is not null)
        {
            context.Hide(_shown);
            _target.Dispose();
        }

        _args.Dispose();
        _things.Dispose();
    }
}
