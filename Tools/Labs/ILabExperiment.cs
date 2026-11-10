using Xanadu.Ecs;

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
    System.Action<bool> ClaimTyping);

/// <summary>
/// What a stage is handed each frame: the time, the pointer in design pixels,
/// whether its left button is down, and the raw MoonWorks input for stages that
/// want the other buttons, the wheel or keys, and the text typed this frame
/// (only while the stage has claimed typing through <see cref="LabContext.ClaimTyping"/>).
/// </summary>
internal readonly record struct LabFrame(float Delta, float Time, Vector2 Mouse, bool Held, MoonWorks.Input.Inputs? Inputs = null, string Typed = "");

/// <summary>One open experiment's drawing. It is made when its tab opens and disposed when another does.</summary>
internal interface ILabStage : System.IDisposable
{
    void Update(in LabFrame frame);

    /// <summary>Records the stage's own GPU passes, before the canvas draws (for stages that render offscreen).</summary>
    void Render(MoonWorks.Graphics.CommandBuffer commands) { }
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

    /// <summary>True to drop the side panel and lay out the whole body (stage and panel room) itself.</summary>
    bool FullWidth => false;

    /// <summary>Short lines shown at the top of the side panel.</summary>
    string[] Notes { get; }

    /// <summary>Adds any controls or readouts the experiment wants under its notes.</summary>
    void BuildPanel(UiNode panel) { }

    /// <summary>Builds the drawing under <paramref name="parent"/>, laid out inside <paramref name="area"/> (design pixels).</summary>
    ILabStage CreateStage(LabContext context, Handle parent, Rect2 area);
}

/// <summary>Every experiment the lab shows, one tab each, in tab order.</summary>
internal static class LabExperiments
{
    public static ILabExperiment[] All(LabsOptions options) =>
    [
        new Experiments.ShaderPlayground(options.Shader),
        new Experiments.ShaderEditor(),
        new Experiments.ParticleBursts(),
        new Experiments.BlendLab(),
        new Experiments.DepthLab(),
        new Experiments.RadianceLab(),
        new Experiments.TransparencyLab(),
        new Experiments.PointerHistoryLab(),
        new Experiments.IndirectDrawLab(),
        new Experiments.RtsLab(),
    ];
}
