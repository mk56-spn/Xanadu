// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.DataStructure;
using XanaduProject.Screens.Player;

namespace XanaduProject.Screens.StageSelection
{
    [SuperNode(typeof(Provider))]
    public partial class StageSelection : Control, IProvide<StageSelection>
    {
        public override partial void _Notification(int what);

        [Export] private Button startButton = null!;
        [Export] private Button editButton = null!;

        StageSelection IProvide<StageSelection>.Value() => this;

        public StageInfo ActiveInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            AddChild(new StageSelectionCarousel());

            startButton.Pressed += () => loadScene(new PlayerLoader(ActiveInfo));
            editButton.Pressed += () =>
            {
                Composer.Composer scene = ResourceLoader.Load<PackedScene>("res://Composer/Composer.tscn")
                    .Instantiate<Composer.Composer>();
                scene.StageInfo = ActiveInfo;
                loadScene(scene);
            };

            Provide();
        }

        private void loadScene(Node scene)
        {
            GetTree().Root.AddChild(scene);
            GetTree().CurrentScene = scene;
            GetTree().Root.RemoveChild(this);
        }
    }
}
