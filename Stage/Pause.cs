// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Screens.StageSelection;

namespace XanaduProject.Stage
{
    public partial class Pause : SubScreen
    {
        private readonly Player player;
        private VBoxContainer buttonContainer = new();
        private Button quit = new() { Text = "Quit", CustomMinimumSize = new Vector2(300, 100)};
        private Button resume = new() { Text = "Resume",  CustomMinimumSize = new Vector2(300, 100)};
        private Button restart = new() { Text = "Restart", CustomMinimumSize = new Vector2(300, 100) };

        public Pause(Player player)
        {
            ZIndex = 3000;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(new ColorRect { Modulate = Colors.Black});
            this.player = player;
            quit.Pressed += () => player.ScreenManager.RequestChangeScreen(new StageSelection());
            resume.Pressed += () =>
            {
                Visible = false;
                player.StageConductor.Clock.TogglePause();
            };
            restart.Pressed += () =>
            {
                player.StageConductor.Clock.Restart();
                player.StageConductor.Clock.Resume();
                Visible = false;
            };

            AddChild(buttonContainer);
            buttonContainer.AddChild(quit);
            buttonContainer.AddChild(resume);
            buttonContainer.AddChild(restart);

            Visible = false;
        }

        protected override void OnShow() => player.StageConductor.Clock.Pause();
        protected override void OnHide() => player.StageConductor.Clock.Resume();


        public override void _Ready()
        {
            base._Ready();
            buttonContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        }
    }
}
