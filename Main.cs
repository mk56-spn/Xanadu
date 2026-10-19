using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Scenes;
using Writer.Systems;

namespace Writer;

// Godot lifecycle only. System wiring lives in WriterSystemRoot.
public partial class Main : Control
{
    public EntityStore Entities { get; } = new();

    [Export] public bool StressTest { get; set; }

    private WriterSystemRoot? _systems;

    public override void _Ready()
    {
        SetProcess(true);
        _systems = new WriterSystemRoot(Entities, GetCanvasItem(), new GodotPointerInputSource(this));

        if (StressTest)
            StressScene.Build(Entities, GetViewportRect().Size);
        else
            TestScene.Build(Entities);
    }

    public override void _Process(double delta)
    {
        float time = (float)Time.GetTicksMsec() / 1000f;
        _systems?.Update(new UpdateTick((float)delta, time));
    }

    public override void _Draw() => DrawCircle(Vector2.Zero, 100, Colors.Red);

    public override void _ExitTree()
    {
        _systems?.Dispose();
        _systems = null;
    }
}
