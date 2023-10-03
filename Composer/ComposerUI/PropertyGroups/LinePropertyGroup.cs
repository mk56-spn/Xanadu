// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;

namespace XanaduProject.Composer.ComposerUI.PropertyGroups
{
    public partial class LinePropertyGroup : PropertyGroup
    {
        protected override string GroupName  => "Line";

        protected override Color GroupColour => Colors.Yellow;

        private Slider width = new HSlider { MinValue = 0, MaxValue = 20 };
        private OptionButton lineCapPicker = new OptionButton();

        public LinePropertyGroup ()
        {
            AddChild(width);
            AddChild(lineCapPicker);

            lineCapPicker.AddItem("None");
            lineCapPicker.AddItem("Flat");
            lineCapPicker.AddItem("Rounded");

            width.ValueChanged += val =>
            {
                foreach (var selectableNode in Selected.OfType<Line2D>())
                    selectableNode.Width = (float)val;
            };


            lineCapPicker.ItemSelected += index =>
            {
                Line2D.LineCapMode result = index switch
                {
                    0 => Line2D.LineCapMode.None,
                    1 => Line2D.LineCapMode.Box,
                    2 => Line2D.LineCapMode.Round,
                    _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
                };

                foreach (var selectableNode in Selected.OfType<Line2D>())
                    selectableNode.EndCapMode = selectableNode.BeginCapMode = result;
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            Visible = Selected.OfType<Line2D>().Any();
        }
    }
}
