// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.DataStructure;
using XanaduProject.Screens.StageUI;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.Player
{
    [SuperNode(typeof(Provider))]
    public partial class PlayerLoader : Node, IProvide<StageInfo>
    {
        public override partial void _Notification(int what);


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

            AudioSource audioSource = GetNode<AudioSource>("/root/GlobalAudio");

            audioSource.SetTrack(stageInfo.TrackInfo);

            PackedScene transitionScene = ResourceLoader.Load<PackedScene>("res://Screens/StageUI/Transition.tscn");
            CanvasLayer layer = new CanvasLayer();

            Transition transition = transitionScene.Instantiate<Transition>();

            AddChild(layer);
            layer.AddChild(transition);

            TreeExited += () => audioSource.Stream = null;

            // Delay player creation until transition is over.
            transition.TransitionFinished += (_, _) => createPlayer(audioSource);

            Provide();
        }

        private void createPlayer(AudioSource audioSource)
        {
            audioSource.Stop();

            Player player = new Player(stageInfo);
            AddChild(player);

            player.PauseMenu.RestartRequest += (_, _) =>
            {
                RemoveChild(player);
                player.QueueFree();
                createPlayer(audioSource);
            };
        }
    }
}
