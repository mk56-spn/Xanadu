// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.UI
{
    public partial class UiMaster : Node, IUiMaster
    {
        public CanvasLayer ScoreLayer { get; private set; } = new();
        public Control TopCenterUi { get; private set; } = new();
        public Control BottomCenterUi { get; private set; } = new();

        public UiMaster()
        {
            AddChild(ScoreLayer);
            ScoreLayer.AddChild(TopCenterUi);
            ScoreLayer.AddChild(BottomCenterUi);
            BottomCenterUi.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
            TopCenterUi.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        }
    }
}
