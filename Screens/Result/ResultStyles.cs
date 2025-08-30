// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.Result
{
    public static class ResultStyles
    {
        public static  readonly StyleBoxFlat INSET = new()
        {
            BgColor = Colors.Gold,
            BorderColor = Colors.Gold.Darkened(0.3f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            ContentMarginBottom = 4,
            ContentMarginTop = 4,
            ContentMarginLeft = 10,
        };
    }
}
