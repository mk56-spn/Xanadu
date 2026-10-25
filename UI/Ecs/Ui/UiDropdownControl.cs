using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Entities created by <see cref="UiBuilder.Dropdown"/>.</summary>
public sealed class UiDropdownControl(
    Entity button,
    Entity list,
    IReadOnlyList<Entity> options)
{
    public Entity Button { get; } = button;
    public Entity List { get; } = list;
    public IReadOnlyList<Entity> Options { get; } = options;
}
