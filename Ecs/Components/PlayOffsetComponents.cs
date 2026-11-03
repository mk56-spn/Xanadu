using Friflo.Engine.ECS;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>This play's map offset, on top of the global one, and whatever is showing it.</summary>
public struct PlayOffset(string levelId, Entity label) : IComponent
{
    /// <summary>The map this play is of, which its offset is saved against.</summary>
    public string LevelId = levelId;

    /// <summary>The on-screen readout, shown for a moment after a change.</summary>
    public Entity Label = label;

    public long MapMs;

    /// <summary>What a replay says the whole offset was at this point; overrides the current settings.</summary>
    public long? ReplayTotalMs;

    public float LabelSeconds;
    public bool Loaded;

    /// <summary>The whole offset a playfield is judged with right now.</summary>
    public static long TotalMs(Entity playfield, OffsetSettings global)
    {
        if (!playfield.TryGetComponent(out PlayOffset offset))
            return global.OffsetMs;

        return offset.ReplayTotalMs ?? global.OffsetMs + offset.MapMs;
    }
}
