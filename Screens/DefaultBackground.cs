// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Tools;

namespace XanaduProject.Screens
{
    public partial class DefaultBackground : Control
    {
        public override void _Ready()=>
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        public override void _Draw()
        {
            this.DrawTransitionLine(Size.X, Size.Y - 100, Size.Y, 200, Colors.Gold);

            this.DrawTransitionLine(Size.X, 50,100, 150, Colors.Gold, 0.6f, fill: true, fillColor: Colors.Gold.Darkened(0.7f) , fillAbove: true);
        }
    }
}
