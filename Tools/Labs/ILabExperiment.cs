using Xanadu.Ecs;
using Xanadu.Host.Tools;

namespace Xanadu.Labs;

/// <summary>
/// What the host lends a stage when it is made: the canvas pipelines, and for
/// stages that render on their own, the GPU and a way to show a texture on the
/// canvas (<see cref="Show"/> mints its id; <see cref="Hide"/> forgets it).
/// </summary>
internal sealed record LabContext(
    Xanadu.Host.Rendering.CanvasPipelines Pipelines,
    MoonWorks.Graphics.GraphicsDevice Device,
    System.Func<MoonWorks.Graphics.Texture, Handle> Show,
    System.Action<Handle> Hide,
    Xanadu.Host.Text.HostText Text,
    Friflo.Engine.ECS.EntityStore Entities);

/// <summary>
/// One isolated experiment: a tab in the lab. The stage draws straight onto
/// canvas items (through <c>CanvasServer</c>); the side panel is ECS UI like
/// everywhere else. Register new experiments in <see cref="LabExperiments.All"/>.
/// </summary>
internal interface ILabExperiment : IToolTab
{
    /// <summary>Builds the drawing under <paramref name="parent"/>, laid out inside <paramref name="area"/> (design pixels).</summary>
    IToolStage CreateStage(LabContext context, Handle parent, Rect2 area);
}

/// <summary>Every experiment the lab shows, one tab each, in tab order.</summary>
internal static class LabExperiments
{
    public static ILabExperiment[] All(LabsOptions options) =>
    [
        new Experiments.ShaderPlayground(options.Shader),
        Experiments.ShaderEditor.Effects(options.EditorFile),
        new Experiments.ParticleBursts(),
        new Experiments.BlendLab(),
        new Experiments.DepthLab(),
        new Experiments.RadianceLab(),
        new Experiments.TransparencyLab(),
        new Experiments.PointerHistoryLab(),
        new Experiments.IndirectDrawLab(),
        Experiments.ShaderEditor.Spawner(),
        new Experiments.RtsLab(),
    ];
}
