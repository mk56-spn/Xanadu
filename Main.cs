using Friflo.Engine.ECS;
using Godot;

namespace Writer;

// Godot lifecycle only. System wiring lives in WriterSystemRoot.
public partial class Main : Control
{
    public EntityStore Entities { get; } = new();

    private WriterSystemRoot? _systems;

    public override void _Ready()
    {
        SetProcess(true);
        _systems = new WriterSystemRoot(Entities, GetCanvasItem());
    }

    public override void _Process(double delta)
    {
        float time = (float)Time.GetTicksMsec() / 1000f;
        _systems?.Update(new UpdateTick((float)delta, time));
    }

    public override void _ExitTree() => _systems = null;
}
