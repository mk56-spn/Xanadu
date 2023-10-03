// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer.ComposerUI.PropertyGroups
{
    public partial class LinePropertyGroup : PropertyGroup
    {
        protected override string GroupName  => "Line";

        private Slider width = new HSlider { MinValue = 0, MaxValue = 20 };

        public LinePropertyGroup ()
        {
            AddChild(width);

            width.ValueChanged += val =>
            {
                foreach (var selectableNode in Selected.OfType<Line2D>())
                    selectableNode.Width = (float)val;
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            Visible = Selected.OfType<Line2D>().Any();
        }
    }
}
