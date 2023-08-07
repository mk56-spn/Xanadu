// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.Player
{
    public partial class PlayerLoader : Node
    {
        private readonly StageInfo stageInfo;

        public PlayerLoader (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
        }

        public override void _Ready()
        {
            base._Ready();

            AudioSource audioSource = GetNode<AudioSource>("/root/GlobalAudio");

            audioSource.SetTrack(stageInfo.TrackInfo);

            createPlayer(audioSource);

            TreeExited += () => audioSource.Stream = null;
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
