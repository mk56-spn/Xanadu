// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;
using XanaduProject.Scenes;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Screens.Settings;

namespace XanaduProject.Screens
{
    public partial class MainMenu : MainScreen
    {
        private HBoxContainer buttons = new() ;
        private readonly AnimatedHoverButton start = new("start")
        {
            CustomMinimumSize = new Vector2(150, 150)
        };
        private readonly AnimatedHoverButton settings = new("Settings")
        {
            CustomMinimumSize = new Vector2(150, 150)

        };
        private readonly AnimatedHoverButton quit = new("Quit")
        {
            CustomMinimumSize = new Vector2(150, 150)
        };

        private readonly AnimatedHoverButton Rigging = new("Rigging")
        {
            CustomMinimumSize = new Vector2(150, 150)
        };


        private  SettingsSubScreen settingsScreen = new();
        public MainMenu()
        {
            Control p = new Control();
            p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(p);
            p.AddChild(buttons);
            buttons.AddChild(start);
            buttons.AddChild(settings);
            buttons.AddChild(Rigging
            );
            buttons.AddChild(quit);

            start.Pressed += () =>
                ScreenManager.RequestChangeScreen(new StageSelection.StageSelection());
            quit.Pressed += () => GetTree().Quit();
            Rigging.Pressed += () => ScreenManager.RequestChangeScreen(new IkRiggingScreen());
            settings.Pressed += () =>
            {
                settingsScreen.Visible = true;
            };

            var gradient = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = [new Color(0.1f, 0.0f, 0.0f, 1.0f), new Color(0.0f, 0.0f, 0.0f, 1.0f)],
                    Offsets = [0.0f, 1.0f]
                },
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0.5f, 0.5f)
            };
        }

        public override void _Ready()
        {
            ScreenManager.ChangeSubScreen(settingsScreen);
            buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        }
    }
}
