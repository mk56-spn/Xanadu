using MoonWorks.Graphics;
using Xanadu.Host.Tools;

namespace Xanadu.Labs;

/// <summary>
/// What the host lends a stage when it is made: the canvas pipelines, and for
/// stages that render on their own, the GPU and a way to show a texture on the
/// canvas (<see cref="Show"/> mints its id; <see cref="Hide"/> forgets it).
/// </summary>
internal sealed record LabContext(
    Xanadu.Rendering.CanvasPipelines Pipelines,
    MoonWorks.Graphics.GraphicsDevice Device,
    System.Func<Texture, Handle> Show,
    System.Action<Handle> Hide,
    Xanadu.Rendering.Text.HostText Text,
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
    public static ILabExperiment[] All() =>
    [
        new Experiments.GameplayLab(),
        new Experiments.ParticleBursts(),
        new Experiments.BlendLab(),
        new Experiments.DepthLab(),
        new Experiments.RadianceLab(),
        new Experiments.TransparencyLab(),
        new Experiments.GlassLab(),
        new Experiments.PointerHistoryLab(),
        new Experiments.Particles.ParticleLab(),
        new Experiments.RtsLab(),
        new Experiments.EcsRender.EcsRenderLab(),
        new Experiments.CjkGridLab(),
        new Experiments.GliderLab(),
        new Experiments.BoosterLab(),
        new Experiments.BeamLab(),
        new Experiments.OrbLab(),
    ];
}
