// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Tools;
using ZLinq.Linq;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelectionPanel : MarginContainer
    {
        public StageInfo Info { get; }
        private GradientTexture2D placeholderTexture;

        public StageSelectionPanel(StageInfo info)
        {
            GD.Print(info.StageName);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;

            Info = info;
            FocusMode = FocusModeEnum.All;
            CustomMinimumSize = new Vector2(300, 300);

            var gradient = new Gradient();
            gradient.Colors = [Colors.Black, new Color(0.1f,0.1f,0.1f)];
            gradient.Offsets = [0.0f, 1.0f];

            placeholderTexture = new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = Vector2.Zero,
                FillTo = Vector2.One / 2f
            };

            AddChild(new Label
            {
                Text = Info.StageName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        public override void _Draw()
        {
            this.DrawSquareWithInsets(new Rect2(Vector2.Zero, Size),
                new Inset
                {
                    InnerLength = 50,
                    OuterLength = 60,
                    InsetDepth = 10,
                    InsetCurveTension = 0,
                },
                new Bevel
                {
                    BevelRadius = 40,
                    BevelSides = Sides.All
                },
                new ShapeStyle
                {
                    Fill = true,
                    FillColor = UiColours.HIGHLIGHT_ONE,
                    Outline = true,
                    OutlineColor = UiColours.MAIN_COLOUR,
                    OutlineWidth = 1
                }, texture: UiColours.GRADIENT);
            var placeholderRect = new Rect2(new Vector2(40, 40), Size - new Vector2(80, 80));

            DrawTextureRect(placeholderTexture, placeholderRect, false);
            DrawRect(new Rect2(new Vector2(40, 40), Size - new Vector2(80, 80)), Colors.White.Darkened(0.8f), false,10);
        }
    }
}
