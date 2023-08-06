// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

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

            createPlayer();
        }

        private void createPlayer()
        {
            Player player = new Player(stageInfo);
            AddChild(player);

            player.PauseMenu.RestartRequest += (_, _) =>
            {
                RemoveChild(player);
                player.QueueFree();
                createPlayer();
            };
        }
    }
}