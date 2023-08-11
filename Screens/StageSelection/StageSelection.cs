// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Screens.Player;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {
        [Export] private Button startButton = null!;
        [Export] private Button editButton = null!;

        public StageInfo ActiveInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            AddChild(new StageSelectionCarousel(this));

            startButton.Pressed += () => loadScene(new PlayerLoader(ActiveInfo));
            editButton.Pressed += () =>
            {
                Composer.Composer scene = ResourceLoader.Load<PackedScene>("res://Composer/Composer.tscn")
                    .Instantiate<Composer.Composer>();
                scene.StageInfo = ActiveInfo;
                loadScene(scene);
            };
        }

        private void loadScene(Node scene)
        {
            GetTree().Root.AddChild(scene);
            GetTree().CurrentScene = scene;
            GetTree().Root.RemoveChild(this);
        }
    }
}
