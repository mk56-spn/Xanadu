using Godot;

namespace XanaduProject.Screens.StageUI;

[GlobalClass]
public partial class StagePause : Control
{
    public StagePause() =>
        Visible = false;

    public override void _Ready()
    {
        base._Ready();

        ButtonActions();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Input.IsActionJustPressed("escape"))
            Show();
    }

    private void ButtonActions()
    {
        GetNode<Button>("ButtonContainer/Play").ButtonUp += () =>
        {
            GetTree().Paused = false;
            Hide();
        };

        GetNode<Button>("ButtonContainer/Restart").ButtonUp += () =>
        {
            Hide();
            GetTree().ReloadCurrentScene();
        };

        GetNode<Button>("ButtonContainer/Quit").ButtonUp += () =>
        {
            Hide();
            GetTree().ChangeSceneToFile("res://Screens/StageSelection/StageSelection.tscn");
        };
    }
}