using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Systems.Playfields;

/// <summary>What every play-note judge, hit and action needs: the store, the keys and the offsets.</summary>
public sealed class PlayNoteContext(EntityStore entities, KeybindSettings keybinds, OffsetSettings offsets)
{
    public EntityStore Entities { get; } = entities;
    public KeybindSettings Keybinds { get; } = keybinds;
    public OffsetSettings Offsets { get; } = offsets;
}
