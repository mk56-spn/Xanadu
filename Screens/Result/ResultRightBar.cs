// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.Screens.Result
{
    public partial class ResultRightBar : Control
    {
        public ResultRightBar(EntityStore store)
        {
            AddChild(new ResultAccuracy(store));
            Size = new Vector2(1000,0);
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                ContentMarginTop = 300,
                ContentMarginLeft = 30,
                BgColor = Colors.Black,
                BorderColor = Colors.Gold,

                BorderWidthBottom = 2,
                BorderWidthLeft = 3,
                BorderWidthRight = 3,
                BorderWidthTop = 2
            });
            SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide, LayoutPresetMode.KeepSize, -200);
        }

        public override void _Ready()
        {
        }
    }
}
