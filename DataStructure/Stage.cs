using Godot;

namespace XanaduProject.DataStructure;

[Tool]
[GlobalClass]
public partial class Stage : WorldEnvironment
{
    private Camera2D camera = new Camera2D();
    private Node2D core;


    public Stage()
    {
        PackedScene coreScene = (PackedScene)ResourceLoader.Load("res://Perceptions/Core.tscn");
        core = (Node2D)coreScene.Instantiate();

        AddChild(core);
        AddChild(camera);
    }


    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        camera.Position = core.Position;
    }
}