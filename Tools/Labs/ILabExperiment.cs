using Godot;
using Xanadu.Ecs;

namespace Xanadu.Labs;

/// <summary>What the host lends a stage when it is made.</summary>
internal sealed record LabContext(Xanadu.Host.Rendering.CanvasPipelines Pipelines);

/// <summary>What a stage is handed each frame: the time, and the pointer in design pixels.</summary>
internal readonly record struct LabFrame(float Delta, float Time, Vector2 Mouse);

/// <summary>One open experiment's drawing. It is made when its tab opens and disposed when another does.</summary>
internal interface ILabStage : System.IDisposable
{
    void Update(in LabFrame frame);
}

/// <summary>
/// One isolated experiment: a tab in the lab. The stage draws straight onto
/// canvas items (through <c>CanvasServer</c>); the side panel is ECS UI like
/// everywhere else. Register new experiments in <see cref="LabExperiments.All"/>.
/// </summary>
internal interface ILabExperiment
{
    /// <summary>The tab label.</summary>
    string Name { get; }

    /// <summary>Short lines shown at the top of the side panel.</summary>
    string[] Notes { get; }

    /// <summary>Adds any controls or readouts the experiment wants under its notes.</summary>
    void BuildPanel(UiNode panel) { }

    /// <summary>Builds the drawing under <paramref name="parent"/>, laid out inside <paramref name="area"/> (design pixels).</summary>
    ILabStage CreateStage(LabContext context, Rid parent, Rect2 area);
}

/// <summary>Every experiment the lab shows, one tab each, in tab order.</summary>
internal static class LabExperiments
{
    public static ILabExperiment[] All(LabsOptions options) =>
    [
        new Experiments.ShaderPlayground(options.Shader),
        new Experiments.ParticleBursts(),
    ];
}
