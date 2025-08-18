// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;
using XanaduProject.Character;

namespace XanaduProject.Screens
{
    public partial class MainMenu : Screen
    {
        private VBoxContainer buttons = new() ;
        private readonly AnimatedHoverButton start = new("start");
        private readonly AnimatedHoverButton settings = new("Settings");
        private readonly AnimatedHoverButton quit = new("Quit");

        public MainMenu()
        {
            buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            AddChild(buttons);
            buttons.AddChild(start);
            buttons.AddChild(settings);
            buttons.AddChild(quit);

            start.Pressed += () =>
                ScreenManager.RequestChangeScreen(GD.Load<PackedScene>("uid://c7dnjjmgr5dhc")
                    .Instantiate<StageSelection.StageSelection>());
            quit.Pressed += () => GetTree().Quit();
            settings.Pressed += () => ScreenManager.InvokeSetting();
        }
    }
}
