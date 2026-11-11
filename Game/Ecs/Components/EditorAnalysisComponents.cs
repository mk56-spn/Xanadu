using Friflo.Engine.ECS;
using Xanadu.Editor.Analysis;

namespace Xanadu.Ecs;

/// <summary>
/// What the editor has worked out about the chart: how dense it is and which
/// notes cannot be reached. Recomputed only when the chart changes.
/// </summary>
public struct EditorAnalysis : IComponent
{
    public int Signature;
    public DensityProfile Density;
    public PlayabilityIssue[] Issues;

    public static EditorAnalysis Create() => new()
    {
        Density = DensityProfile.Empty,
        Issues = [],
    };
}
