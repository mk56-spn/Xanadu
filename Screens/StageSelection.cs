using Godot;

namespace XanaduProject.Screens;

public partial class StageSelection : Control
{
    public override void _Ready()
    {
        base._Ready();

        GetNode<Button>("HBoxContainer/Button").ButtonDown += () => GetTree().ChangeSceneToFile("res://Resources/Stages/TestStage.tscn");
        GetNode<Button>("HBoxContainer/Button2").ButtonDown += () => GetTree().ChangeSceneToFile("res://Resources/Stages/TestStage2.tscn");
    }
}