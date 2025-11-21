// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Factories;
using XanaduProject.Factories.ShaderFactoryHelpers;
using XanaduProject.Stage.Masters.Rendering;
using XanaduProject.Tools;

namespace XanaduProject.Screens
{
    public partial class DefaultBackground : Container
    {
        private static readonly Color main_colour = Colors.Gold.Darkened(0.4f);
        private static readonly Color secondary_colour = Colors.Gold.Darkened(0.6f);
        private static readonly Color terciary_colour = Colors.Gold.Darkened(0.8f);
        public override void _Ready(){
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var can = RenderRid.Create(this)
                .SetTransform(new Vector2(200, 200))
                .AddRect(new Vector2(100, 100));

            Modulate = Colors.Red;

            can.SetMaterial(BlockMaterials.GetMaterial(BlockShaderId.SquareInSquare).GetRid());
        }

        private const float curve_width = 0.2f;
        private const float curve_height = 0.1f;

        public override void _Draw()
        {
            this.DrawTransitionLine(Size.X, Size.Y - Size.Y * curve_height, Size.Y, curve_width * Size.Y, main_colour, fill: true, fillColor: secondary_colour);

            this.DrawTransitionLine(Size.X, 0,Size.Y * curve_height, curve_width * Size.Y, main_colour, 0.6f, fill: true, fillColor: secondary_colour , fillAbove: true);
        }
    }
}
