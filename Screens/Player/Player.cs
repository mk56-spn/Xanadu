// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Screens.StageUI;

namespace XanaduProject.Screens.Player
{
    [SuperNode(typeof(Provider))]
    public partial class Player : Control, IProvide<TrackHandler>
    {
        public override partial void _Notification(int what);

        public TrackHandler Value() => trackHandler;

        private TrackHandler trackHandler = new TrackHandler();
        private readonly StageInfo stageInfo;
        private Stage stage = null!;

        private Camera2D camera = new Camera2D();
        public readonly StagePause PauseMenu = ResourceLoader.Load<PackedScene>("res://Screens/StageUI/StagePause.tscn").Instantiate<StagePause>();

        public Player (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
        }

        public override void _Ready()
        {
            trackHandler.SetTrack(stageInfo.TrackInfo);
            AddChild(trackHandler);

            Provide();

            base._Ready();

            stage = stageInfo.Stage.Instantiate<Stage>();

            CanvasLayer canvasLayer = new CanvasLayer();

            AddChild(camera);
            AddChild(stage);
            camera.AddChild(canvasLayer);
            canvasLayer.AddChild(PauseMenu);

            trackHandler.StartTrack();
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            camera.Position = stage.Core.Position;

            // Pause menu handling
            base._Process(delta);

            if (!stage.Core.IsAlive && !PauseMenu.Visible)
            {
                PauseMenu.Show();
                return;
            }

            if (!Input.IsActionJustPressed("escape")) return;

            GetTree().Paused = true;
            PauseMenu.Show();
        }
    }
}
