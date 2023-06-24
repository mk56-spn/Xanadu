using Godot;
using XanaduProject.Perceptions;

namespace XanaduProject.Stages;

public partial class TestStage : Node2D
{
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        GetNode<Camera2D>("Camera").Position = GetNode<Perception>("Core").Position;
    }
}