// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Stage;
using MainScreen = XanaduProject.Screens.ScreenStructure.MainScreen;

namespace XanaduProject.Screens.Result
{
    public partial class ResultScreen : MainScreen
    {
        private readonly VBoxContainer info = new();

        public ResultScreen(Player player, ScoreCalculator scoreCalculator)
        {
            EntityStore store = player.EntityStore;

            BackgroundOverride = new ResultBackGround();
            AddChild(new ResultRightBar(scoreCalculator));
            info.AddChild(new ResultGraph(store));
            info.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide, margin: 90);
            Color = UiColours.MAIN_COLOUR_DARK;


            AddChild(info);
            info.AddThemeConstantOverride("separation", 10);

            info.AddChild(new ResultTally(scoreCalculator));

            info.AddChild(new ResultMisc(scoreCalculator));

            AddChild(new ResultFooter(ScreenManager, player));
        }
    }
}
