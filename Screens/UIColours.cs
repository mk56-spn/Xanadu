// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens
{
    public static class UiColours
    {
        public static readonly Color HIGHLIGHT_ONE = Colors.DarkSlateGray.Lightened(0.3f);
        public static readonly Color HIGHLIGHT_ONE_DARK = Colors.DarkSlateGray;

        public static readonly Color HIGHLIGHT_TWO = Colors.Brown.Lightened(0.3f);
        public static readonly Color HIGHLIGHT_TWO_DARK = Colors.Brown;

        public static readonly Color GREY1 = Colors.DimGray;
        public static readonly Color GREY2 = Colors.DimGray.Darkened(0.3f);
        public static readonly Color GREY3 = Colors.DimGray.Darkened(0.6f);

        public static readonly Color MAIN_COLOUR = Colors.Gold;
        public static readonly Color MAIN_COLOUR_DARK = Colors.Gold.Darkened(0.3f);

        public static readonly GradientTexture2D GRADIENT = new()
        {
            FillFrom = new Vector2(0, 0),
            FillTo = new Vector2(0, 1),
            Gradient = new Gradient
            {
                InterpolationMode = Gradient.InterpolationModeEnum.Cubic,
                Offsets = [0, 0.5F, 1],
                Colors = [Colors.White.Darkened(0.2f), Colors.White, Colors.White.Darkened(0.2f)]
            }
        };
    }
}
