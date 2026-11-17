using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>What every play-note judge, hit and action needs: the store, the keys and the offsets.</summary>
public sealed class PlayNoteContext(EntityStore entities, KeybindSettings keybinds, OffsetSettings offsets)
{
    public EntityStore Entities { get; } = entities;
    public KeybindSettings Keybinds { get; } = keybinds;
    public OffsetSettings Offsets { get; } = offsets;
}
