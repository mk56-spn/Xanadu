using Godot;

namespace XanaduProject.Composer;

[GlobalClass]
public partial class Stage : WorldEnvironment
{
    private Camera2D _camera = new Camera2D();
    private Node2D _core;


    public Stage()
    {
        PackedScene coreScene = (PackedScene)ResourceLoader.Load("res://Perceptions/Core.tscn");
        _core =(Node2D)coreScene.Instantiate();

        AddChild(_core);
        AddChild(_camera);
    }


    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        _camera.Position = _core.Position;
    }
}