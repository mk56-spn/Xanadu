// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelectionPanel : PanelContainer
    {
        private readonly StageInfo stageInfo;

        public StageSelectionPanel(StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
        }

        public override void _Ready()
        {
            base._Ready();

            var label = new Label { Text = stageInfo.Title };
            var selectButton = new Button { Text = "PLAY" };
            var container = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };

            AddChild(container);
            container.AddChild(label);
            container.AddChild(selectButton);

            selectButton.Pressed += () =>
            {
                GetNode<AudioSource>("/root/GlobalAudio").SetTrack(stageInfo.TrackInfo);
                GetTree().ChangeSceneToPacked(stageInfo.Stage);
            };
        }
    }
}
