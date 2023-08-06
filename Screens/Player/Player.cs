// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.Player
{
    public partial class Player : Control
    {
        private readonly StageInfo stageInfo;

        public Player (StageInfo stageInfo)
        {
            this.stageInfo = stageInfo;
        }

        public override void _Ready()
        {
            base._Ready();

            AddChild(stageInfo.Stage.Instantiate<Stage>());
        }
    }
}
