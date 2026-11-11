using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Host.Tools;

namespace Xanadu.Labs;

/// <summary>
/// Xanadu Labs on the MoonWorks host: a tab per experiment, in the shared
/// tabbed tool shell (<see cref="TabbedToolApp"/>): its notes and controls in
/// an ECS side panel, its drawing on the stage to the right.
/// </summary>
internal sealed class LabsApp : TabbedToolApp
{
    private readonly ILabExperiment[] _experiments;

    public LabsApp(LabsOptions options) : this(options, LabExperiments.All(options))
    {
    }

    private LabsApp(LabsOptions options, ILabExperiment[] experiments) : base("Xanadu Labs",
        new HostWindowOptions(options.WindowWidth, options.WindowHeight, options.ShotPath, options.ShotFrame),
        experiments,
        options.Tab)
    {
        _experiments = experiments;
    }

    protected override IToolStage CreateStage(int tab, Handle parent, Rect2 area) =>
        _experiments[tab].CreateStage(
            new LabContext(
                Renderer.Pipelines, GraphicsDevice, ShowTexture, id => Renderer.Textures.Remove(id.Id),
                Text, Entities),
            parent,
            area);

    private Handle ShowTexture(MoonWorks.Graphics.Texture texture)
    {
        Handle id = Canvas.MintRid();
        Renderer.Textures[id.Id] = texture;
        return id;
    }
}
