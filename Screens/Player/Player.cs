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
    public partial class Player : Control, IProvide<TrackHandler>, IProvide<ScoreProcessor>
    {
        public override partial void _Notification(int what);

        public TrackHandler Value() => trackHandler;
        ScoreProcessor IProvide<ScoreProcessor>.Value() => scoreProcessor;

        private ScoreProcessor scoreProcessor = null!;
        private TrackHandler trackHandler = new TrackHandler();
        private readonly StageInfo stageInfo;
        private Stage stage;

        private Camera2D camera = new Camera2D();
        public readonly StagePause PauseMenu = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/StagePause.tscn").Instantiate<StagePause>();
        public readonly ComboCounter ComboCounter  = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/ComboCounter.tscn").Instantiate<ComboCounter>();

        public Player (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
            stage = stageInfo.GetStage();
            AddChild(stage);
        }

        public override void _Ready()
        {
            base._Ready();
            trackHandler.SetTrack(stageInfo.TrackInfo);

            AddChild(scoreProcessor = new ScoreProcessor(stage));
            AddChild(trackHandler);

            Provide();

            AddChild(camera);
            loadUi();
            trackHandler.StartTrack();
        }

        private void loadUi()
        {
            CanvasLayer canvasLayer = new CanvasLayer();

            camera.AddChild(canvasLayer);
            canvasLayer.AddChild(PauseMenu);
            canvasLayer.AddChild(ComboCounter);
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
