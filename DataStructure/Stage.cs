// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Perceptions;
using XanaduProject.Screens.StageUI;

namespace XanaduProject.DataStructure
{
    [Tool]
    [GlobalClass]
    public partial class Stage : WorldEnvironment
    {
        private Camera2D camera = new Camera2D();
        private Core core;
        private StagePause pauseMenu;

        public Stage()
        {
            var coreScene = ResourceLoader.Load<PackedScene>("res://Perceptions/Core.tscn");
            var pauseMenuScene = ResourceLoader.Load<PackedScene>("res://Screens/StageUI/StagePause.tscn");
            var canvasLayer = new CanvasLayer();

            AddChild(core = (Core)coreScene.Instantiate());
            AddChild(camera);

            camera.AddChild(canvasLayer);
            canvasLayer.AddChild(pauseMenu = (StagePause)pauseMenuScene.Instantiate());
        }


        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            camera.Position = core.Position;
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (!core.IsAlive && !pauseMenu.Visible)
            {
                pauseMenu.Show();
                return;
            }

            if (!Input.IsActionJustPressed("escape")) return;

            GetTree().Paused = true;
            pauseMenu.Show();
        }
    }
}
