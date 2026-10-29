using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// The offset calibration wizard's live handles and run state. The system
/// owns the click-track playback and writes back through these entities.
/// </summary>
public struct OffsetWizard(
    Entity offsetLabel,
    Entity statusLabel,
    Entity start,
    Entity save,
    Entity reset) : IComponent
{
    public Entity OffsetLabel = offsetLabel;
    public Entity StatusLabel = statusLabel;
    public Entity Start = start;
    public Entity Save = save;
    public Entity Reset = reset;
    public List<double> Samples = new();
    public double Average;
    public bool Listening;
    public bool Initialized;
}
