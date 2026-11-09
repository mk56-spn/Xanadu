using System;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Xanadu.Theme;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Host;

/// <summary>
/// The MoonWorks host: owns the window and the GPU and runs the frame. For now it
/// only clears to the game's background colour (step 1 of docs/moonworks-port.md);
/// the renderer and the ECS world come in step 2.
/// </summary>
/// <remarks>
/// Everything draws into <see cref="_target"/>, which is then blitted to the window:
/// swapchain textures can't be read back, and screenshots need to.
/// </remarks>
internal sealed class HostGame : Game
{
    private const uint Width = 1920, Height = 1080;

    private readonly HostOptions _options;
    private readonly Texture _target;
    private int _frames;

    public HostGame(HostOptions options) : base(
        new AppInfo("Xanadu", "Xanadu"),
        new WindowCreateInfo("Xanadu", options.WindowWidth, options.WindowHeight, ScreenMode.Windowed),
        FramePacingSettings.CreateCapped(60, 1),
        ShaderFormat.SPIRV,
        debugMode: false)
    {
        _options = options;
        _target = Texture.Create2D(GraphicsDevice, "frame", Width, Height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
    }

    protected override void Step() { }

    protected override void Update(TimeSpan delta)
    {
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

        // Palette comes from the game assemblies, and its Godot Color struct works without the engine.
        Godot.Color background = Palette.Background;
        RenderPass pass = commands.BeginRenderPass(
            new ColorTargetInfo(_target, new MwColor(background.R, background.G, background.B, 1f)));
        commands.EndRenderPass(pass);

        commands.Blit(_target, swapchain, Filter.Linear);

        _frames++;
        if (_options.ShotPath is { } path && _frames == _options.ShotFrame)
        {
            Screenshot.Save(GraphicsDevice, commands, _target, Width, Height, path);
            Quit();
            return;
        }

        GraphicsDevice.Submit(commands);
    }
}
