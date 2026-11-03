using Godot;
using Xanadu.Ecs;

namespace Xanadu.Lab;

/// <summary>
/// One isolated experiment: a tab in the lab. The stage is plain Godot drawing
/// (shaders, canvas items, nodes); the side panel is ECS UI like everywhere else.
/// Register new experiments in <see cref="LabExperiments.All"/>.
/// </summary>
public interface ILabExperiment
{
    /// <summary>The tab label.</summary>
    string Name { get; }

    /// <summary>Short lines shown at the top of the side panel.</summary>
    string[] Notes { get; }

    /// <summary>The shader (or any source file) the side panel lists read-only; null for none.</summary>
    string? SourcePath => null;

    /// <summary>Builds the experiment's drawing, laid out inside <paramref name="area"/> (design pixels).</summary>
    Node2D CreateStage(Rect2 area);

    /// <summary>Height of what <see cref="BuildPanel"/> adds, so the source listing leaves room for it.</summary>
    float ControlsHeight => 0;

    /// <summary>Adds any controls the experiment wants under its notes.</summary>
    void BuildPanel(UiNode panel) { }
}
