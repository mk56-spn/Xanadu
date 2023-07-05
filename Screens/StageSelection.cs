using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens;

public partial class StageSelection : Control
{
    public override void _Ready()
    {
        base._Ready();

        StageInfo stageInfo1 = ResourceLoader.Load<StageInfo>("res://Resources/Stages/Stage 1/Stage 1.tres");
        StageInfo stageInfo2 = ResourceLoader.Load<StageInfo>("res://Resources/Stages/Stage 2/Stage 2.tres");

        GetNode<Button>("HBoxContainer/Button").ButtonDown += () => GetTree().ChangeSceneToPacked(stageInfo1.Stage);
        GetNode<Button>("HBoxContainer/Button2").ButtonDown += () => GetTree().ChangeSceneToPacked(stageInfo2.Stage);
    }
}