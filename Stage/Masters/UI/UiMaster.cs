// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.UI
{
    public partial class UiMaster : Node, IUiMaster
    {
        public CanvasLayer ScoreLayer { get; private set; } = new();

        public override void _Ready()
        {
            AddChild(ScoreLayer);

        }
    }
}
