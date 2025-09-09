// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Factories;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public partial class OpeningScreen : MainScreen
    {
        public OpeningScreen()
        {
            BackgroundOverride = new Control();

            Label label;
            AddChild(label = new Label
            {
                Text = "Xanadu",
                LabelSettings = new LabelSettings
                {
                    Font = FontSource.PLASTIC_SLANTED,
                    FontSize = 200,
                }
            });


            Ready += () => label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);

            canvas.SetParent(GetCanvasItem());
            Draw += () => canvas.SetTransform(new Transform2D(0, Size / 2).Translated(new Vector2(0, 300)));
        }

        private readonly RenderRid canvas = RenderRid.Create()
            .AddRectOutline(new Vector2(2000,50), width: 2)
            .AddString(new Vector2(0, 10),"PRESS ENTER TO START", 30, font: FontSource.LINE_BOLD)
            .AddParticles(ParticlesRid.Create());

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { KeyLabel: Key.Enter })
            {
                ScreenManager.RequestChangeScreen(screen: new MainMenu(), transitionType: TransitionType.Fade);
            }
        }
    }
}
