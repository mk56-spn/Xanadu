using System;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Lab.MoonWorksParticles;

/// <summary>
/// The simplest GPU particles: one storage buffer of particles, one compute
/// dispatch per frame to move them, one instanced draw of quads that read the
/// same buffer. The CPU only uploads the starting state once.
/// </summary>
internal sealed class ParticleGame : Game
{
    private const uint Count = 200_000;
    private const uint ThreadsPerGroup = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct Particle
    {
        public float X, Y, Vx, Vy;
        public float Age, Lifespan, Hue, Unused;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StepUniforms { public float Width, Height, Dt, Time; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Size, Pad; }

    private const uint Width = 1280, Height = 720;

    private readonly Buffer _particles;
    private readonly Texture _target;
    private readonly ComputePipeline _step;
    private readonly GraphicsPipeline _draw;
    private float _time;
    private float _dt;
    private readonly string? _screenshotPath;
    private int _frames;

    public ParticleGame(string? screenshotPath) : base(
        new AppInfo("Xanadu", "MoonWorksParticles"),
        new WindowCreateInfo("MoonWorks GPU particles", Width, Height, ScreenMode.Windowed),
        FramePacingSettings.CreateCapped(60, 1),
        ShaderFormat.SPIRV,
        debugMode: false)
    {
        _screenshotPath = screenshotPath;
        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "particles.slang");

        _step = ComputePipeline.Create(GraphicsDevice, SlangShaders.Compile(module, "simulate", "compute"), "main",
            new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = ThreadsPerGroup, ThreadCountY = 1, ThreadCountZ = 1,
            });

        Shader vert = Shader.Create(GraphicsDevice, SlangShaders.Compile(module, "quad", "vertex"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumStorageBuffers = 1, NumUniformBuffers = 1 });
        Shader frag = Shader.Create(GraphicsDevice, SlangShaders.Compile(module, "glowDot", "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment });

        _draw = GraphicsPipeline.Create(GraphicsDevice, new GraphicsPipelineCreateInfo
        {
            VertexShader = vert,
            FragmentShader = frag,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription { Format = TextureFormat.R8G8B8A8Unorm, BlendState = ColorTargetBlendState.Additive },
                ],
            },
        });
        // Everything draws into an offscreen texture that is then blitted to the window,
        // which is the same "render to a buffer, then use it" step Godot's back buffers do.
        _target = Texture.Create2D(GraphicsDevice, "target", Width, Height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);

        vert.Dispose();
        frag.Dispose();

        // Start every particle "already dead" at a staggered age so the fountain fills in smoothly.
        var start = new Particle[Count];
        var random = new Random(1);
        for (int i = 0; i < start.Length; i++)
        {
            start[i].Lifespan = 1f;
            start[i].Age = 1f - (float)random.NextDouble() * 3f;
            start[i].X = start[i].Y = -100f;
        }

        var uploader = new ResourceUploader(GraphicsDevice);
        _particles = uploader.CreateBuffer<Particle>("particles", start,
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead);
        uploader.UploadAndWait();
        uploader.Dispose();
    }

    protected override void Step() { }

    protected override void Update(TimeSpan delta)
    {
        _dt = (float)delta.TotalSeconds;
        _time += _dt;
        if (Inputs.Keyboard.IsPressed(KeyCode.Escape))
            Quit();
    }

    protected override void Draw(double alpha)
    {
        CommandBuffer commands = GraphicsDevice.AcquireCommandBuffer();
        Texture? swapchain = commands.AcquireSwapchainTexture(MainWindow);
        if (swapchain is null)
        {
            GraphicsDevice.Submit(commands);
            return;
        }

        // 1. Move the particles on the GPU.
        commands.PushComputeUniformData(new StepUniforms { Width = Width, Height = Height, Dt = _dt, Time = _time });
        ComputePass compute = commands.BeginComputePass(new StorageBufferReadWriteBinding(_particles));
        compute.BindComputePipeline(_step);
        compute.Dispatch((Count + ThreadsPerGroup - 1) / ThreadsPerGroup, 1, 1);
        commands.EndComputePass(compute);

        // 2. Draw them into the offscreen target: 6 vertices per instance, one instance per particle.
        commands.PushVertexUniformData(new ViewUniforms { Width = Width, Height = Height, Size = 2.5f });
        RenderPass render = commands.BeginRenderPass(new ColorTargetInfo(_target, new Color(8, 6, 14)));
        render.BindGraphicsPipeline(_draw);
        render.BindVertexStorageBuffers(_particles);
        render.DrawPrimitives(6, Count, 0, 0);
        commands.EndRenderPass(render);

        // 3. Show it.
        commands.Blit(_target, swapchain, Filter.Linear);

        _frames++;
        if (_screenshotPath is not null && _frames == 150)
        {
            Screenshot.Save(GraphicsDevice, commands, _target, Width, Height, _screenshotPath);
            Quit();
            return;
        }

        GraphicsDevice.Submit(commands);
    }
}
