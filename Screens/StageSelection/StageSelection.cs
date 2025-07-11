// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {

        [Export] private Button startButton = null!;
        [Export] private Button editButton = null!;


        public string Level = null!;

        public override void _Ready()
        {
            AddChild(new StageSelectionCarousel(this));

            startButton.Pressed += () => GetTree().ChangeSceneToPacked(GD.Load<PackedScene>("uid://biel0jq32rdnw"));
            editButton.Pressed += () => GetTree().ChangeSceneToPacked(GD.Load<PackedScene>("uid://dfy4y23ujnsal"));
        }

        private void loadScene(Node scene)
        {
            GetTree().Root.AddChild(scene);
            GetTree().CurrentScene = scene;
            GetTree().Root.RemoveChild(this);
        }
    }
}
