// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Character;
using XanaduProject.Stage;
using MainScreen = XanaduProject.Screens.ScreenStructure.MainScreen;

namespace XanaduProject.Screens.Result
{
    public partial class ResultScreen : MainScreen
    {
        private readonly VBoxContainer info = new();

        public ResultScreen(Player player)
        {
            EntityStore store = player.EntityStore;

            AddChild(new ResultRightBar(store));
            info.AddChild(new ResultGraph(store));
            info.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide, margin: 90);
            Color = Colors.Gold.Darkened(0.3f);


            AddChild(info);
            info.AddThemeConstantOverride("separation", 10);

            info.AddChild(new ResultTally(store));

            info.AddChild(new ResultMisc(store));

            AddChild(new ResultFooter(ScreenManager, player));
        }
    }
}
