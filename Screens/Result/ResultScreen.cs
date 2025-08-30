// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Character;

namespace XanaduProject.Screens.Result
{
    public partial class ResultScreen : Screen
    {
        private readonly VBoxContainer info = new();

        public ResultScreen(EntityStore store)
        {
            AddChild(new ResultBackGround());

            AddChild(new ResultRightBar(store));
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            info.AddChild(new ResultGraph(store));
            info.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide, margin: 90);
            Color = Colors.Gold.Darkened(0.3f);


            AddChild(info);
            info.AddThemeConstantOverride("separation", 10);

            info.AddChild(new ResultTally(store));

            info.AddChild(new ResultMisc(store));

            AddChild(new ResultFooter(ScreenManager, store));
        }
    }
}
