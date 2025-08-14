// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Screens.StageSelection;

namespace XanaduProject.Stage
{
    public partial class Pause : VBoxContainer
    {
        private readonly Player player;
        private Button quit = new() { Text = "Quit", CustomMinimumSize = new Vector2( 200,200 )};
        private Button resume = new() { Text = "Resume",  CustomMinimumSize = new Vector2( 200,200 )};
        private Button restart = new() { Text = "Restart", CustomMinimumSize = new Vector2(200, 200) };

        public Pause(Player player)
        {
            ZIndex = 3000;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(new ColorRect { Modulate = Colors.Black});
            this.player = player;
            quit.Pressed += () => player.ScreenManager.RequestChangeScreen(GD.Load<PackedScene>("uid://c7dnjjmgr5dhc").Instantiate<StageSelection>());
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

            AddChild(quit);
            AddChild(resume);
            AddChild(restart);

            Visible = false;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { Keycode: Key.Escape, Pressed: true })
            {
                Visible = true;
                player.StageConductor.Clock.Pause();
            }

        }
    }
}
