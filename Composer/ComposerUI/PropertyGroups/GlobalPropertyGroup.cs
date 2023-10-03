// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer.ComposerUI.PropertyGroups
{
    /// <summary>
    /// Properties applicable to all composable nodes. Will always be shown in composer as long as some node is selected.
    /// </summary>
    public partial class GlobalPropertyGroup : PropertyGroup
    {
        protected override string GroupName => "Global";

        private SpinBox layerBox;
        private ColorPicker colourPicker;

        public GlobalPropertyGroup ()
        {
            layerBox = new SpinBox { Step = 1, MinValue = -100 };
            colourPicker = new ColorPicker
            {
                ColorMode = ColorPicker.ColorModeType.Hsv,
                SlidersVisible = false,
                ColorModesVisible = false,
                PresetsVisible = false,
                // TODO : Broken rn. needs fix
                SamplerVisible = false,
            };

            AddChild(layerBox);
            AddChild(colourPicker);

            layerBox.ValueChanged += val =>
            {
                foreach (var selectableNode in Selected.OfType<Node2D>())
                    selectableNode.ZIndex = (int)val;
            };

            colourPicker.ColorChanged += val =>
            {
                foreach (var selectableNode in Selected.OfType<Node2D>())
                    selectableNode.SelfModulate = val;
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            Visible = Selected.Any();
        }
    }
}
