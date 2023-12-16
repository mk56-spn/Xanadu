using Godot;

namespace XanaduProject.Screens.Title
{
    public partial class Title : CanvasLayer
    {
        [Export] private Button button = null!;

        // Called when the node enters the scene tree for the first time.
        public override void _Ready() => button.Pressed += () =>
            GetTree().ChangeSceneToFile("res://Screens/StageSelection/StageSelection.tscn");
    }
}
