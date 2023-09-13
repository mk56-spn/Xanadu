// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens.StageUI;

namespace XanaduProject.Screens.Player
{
    [SuperNode(typeof(Provider))]
    public partial class Player : StageHandler, IProvide<ScoreProcessor>
    {
        public override partial void _Notification(int what);

        ScoreProcessor IProvide<ScoreProcessor>.Value() => scoreProcessor;
        private ScoreProcessor scoreProcessor = null!;

        private readonly StagePause pauseMenu = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/StagePause.tscn").Instantiate<StagePause>();

        private readonly ComboCounter comboCounter  = ResourceLoader
            .Load<PackedScene>("res://Screens/StageUI/ComboCounter.tscn").Instantiate<ComboCounter>();

        public Player() : base(new Camera2D())
        {
        }

        public override void _Ready()
        {
            base._Ready();

            Provide();

            AddChild(scoreProcessor = new ScoreProcessor(Stage));

            loadUi();

            Stage.Core.OnDeath += () => pauseMenu.Show();
            pauseMenu.RestartRequest += () => GetParent<PlayerLoader>().LoadPlayer();
        }

        private void loadUi()
        {
            CanvasLayer canvasLayer = new CanvasLayer();

            Camera.AddChild(canvasLayer);
            canvasLayer.AddChild(pauseMenu);
            canvasLayer.AddChild(comboCounter);
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            Camera.Position = Stage.Core.Position;
        }
    }
}
