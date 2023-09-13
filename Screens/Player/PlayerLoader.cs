// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.DataStructure;
using XanaduProject.Screens.StageUI;

namespace XanaduProject.Screens.Player
{
    [SuperNode(typeof(Provider))]
    public partial class PlayerLoader : Node, IProvide<StageInfo>
    {
        public override partial void _Notification(int what);

        private Player? player;
        private readonly StageInfo stageInfo;

        // Cache StageInfo at a loader level for downstream consumption
        StageInfo IProvide<StageInfo>.Value() => stageInfo;

        public PlayerLoader (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
        }

        public override void _Ready()
        {
            base._Ready();

            PackedScene transitionScene = ResourceLoader.Load<PackedScene>("res://Screens/StageUI/Transition.tscn");
            CanvasLayer layer = new CanvasLayer();

            Transition transition = transitionScene.Instantiate<Transition>();

            AddChild(layer);
            layer.AddChild(transition);

            // Delay player creation until transition is over.
            transition.TransitionFinished += (_, _) => LoadPlayer();

            Provide();

            TreeExiting += () => GetTree().Paused = false;
        }

        /// <summary>
        /// Resets the player to it's initial state
        /// </summary>
        public void LoadPlayer()
        {
            player?.QueueFree();
            player = new Player(stageInfo);
            AddChild(player);
        }
    }
}
