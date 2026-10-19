#nullable enable
using Friflo.Engine.ECS;
using Godot;

namespace Writer;

// Clean slate. Everything from here is built on the ECS store.
public partial class Main : Control
{
	public EntityStore Entities { get; } = new();
}
