using Friflo.Engine.ECS;
using Xanadu.Storage.Settings;

namespace Xanadu.Logic.Playfields;

/// <summary>What every play-note judge, hit and action needs: the store, the keys and the offsets.</summary>
public sealed class PlayNoteContext(EntityStore entities, KeybindSettings keybinds, OffsetSettings offsets)
{
    public EntityStore Entities { get; } = entities;
    public KeybindSettings Keybinds { get; } = keybinds;
    public OffsetSettings Offsets { get; } = offsets;
}
