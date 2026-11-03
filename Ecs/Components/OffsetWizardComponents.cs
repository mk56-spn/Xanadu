using Friflo.Engine.ECS;
using Xanadu.Systems.Scenes;

namespace Xanadu.Ecs;

/// <summary>
/// The offset calibration screen: its handles and the run in progress. The system
/// owns the click track and writes back through these entities.
/// </summary>
public struct OffsetWizard(
    Entity offsetLabel,
    Entity resultLabel,
    Entity statusLabel,
    Entity plot,
    Entity[] tapMarks,
    Entity resultMark,
    Entity start,
    Entity apply,
    Entity reset,
    Entity nudgeDown,
    Entity nudgeUp) : IComponent
{
    public Entity OffsetLabel = offsetLabel;
    public Entity ResultLabel = resultLabel;
    public Entity StatusLabel = statusLabel;

    /// <summary>The judgement bands; zero error is at its centre.</summary>
    public Entity Plot = plot;

    public Entity[] TapMarks = tapMarks;
    public Entity ResultMark = resultMark;
    public Entity Start = start;
    public Entity Apply = apply;
    public Entity Reset = reset;
    public Entity NudgeDown = nudgeDown;
    public Entity NudgeUp = nudgeUp;

    public OffsetCalibration Run = new();
    public bool Listening;
    public bool Initialized;
}
