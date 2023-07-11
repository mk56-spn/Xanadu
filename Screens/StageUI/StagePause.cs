// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.StageUI
{
    [GlobalClass]
    public partial class StagePause : Control
    {
        public StagePause()
        {
            Visible = false;
        }

        public override void _Ready()
        {
            base._Ready();

            buttonActions();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (Input.IsActionJustPressed("escape"))
                Show();
        }

        private void buttonActions()
        {
            GetNode<Button>("ButtonContainer/Play").ButtonUp += () =>
            {
                unpause();
                Hide();
            };

            GetNode<Button>("ButtonContainer/Restart").ButtonUp += () =>
            {
                unpause();
                GetTree().ReloadCurrentScene();
            };

            GetNode<Button>("ButtonContainer/Quit").ButtonUp += () =>
            {
                unpause();
                GetTree().ChangeSceneToFile("res://Screens/StageSelection/StageSelection.tscn");
            };
        }

        private void unpause()
        {
            GetTree().Paused = false;
        }
    }
}
