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
        private readonly Stage stage;

        private Camera2D camera = new Camera2D();

        private readonly StagePause pauseMenu = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/StagePause.tscn").Instantiate<StagePause>();

        private readonly ComboCounter comboCounter  = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/ComboCounter.tscn").Instantiate<ComboCounter>();

        public Player (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
            ProcessMode = ProcessModeEnum.Always;

            stage = stageInfo.GetStage();
            AddChild(stage);
        }

        public override void _Ready()
        {
            base._Ready();
            trackHandler.SetTrack(stageInfo.TrackInfo);
            GetTree().Paused = true;

            AddChild(scoreProcessor = new ScoreProcessor(stage));
            AddChild(trackHandler);

            Provide();

            AddChild(camera);
            loadUi();
            trackHandler.StartTrack();

            // We do not want the stage to process whilst the preempt is still underway.
            trackHandler.OnPreemptComplete += (_, _) => GetTree().Paused = false;
            stage.Core.OnDeath += () => pauseMenu.Show();
            pauseMenu.RestartRequest += () => GetParent<PlayerLoader>().LoadPlayer();
        }

        private void loadUi()
        {
            CanvasLayer canvasLayer = new CanvasLayer();

            camera.AddChild(canvasLayer);
            canvasLayer.AddChild(pauseMenu);
            canvasLayer.AddChild(comboCounter);
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            camera.Position = stage.Core.Position;
        }
    }
}
