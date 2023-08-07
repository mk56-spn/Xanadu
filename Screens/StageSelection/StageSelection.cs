// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Screens.Player;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {
        [Export] private Button startButton = null!;

        public StageInfo ActiveInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            AddChild(new StageSelectionCarousel(this));

            startButton.Pressed += () =>
            {
                GetNode<AudioSource>("/root/GlobalAudio").SetTrack(ActiveInfo.TrackInfo);
                loadStage();
            };
        }

        private void loadStage()
        {
            PlayerLoader player = new PlayerLoader(ActiveInfo);

            GetTree().Root.AddChild(player);
            GetTree().CurrentScene = player;
            GetTree().Root.RemoveChild(this);
        }
    }
}
