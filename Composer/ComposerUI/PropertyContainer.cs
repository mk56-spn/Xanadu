// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;

namespace XanaduProject.Composer.ComposerUI
{
    [SuperNode(typeof(Dependent))]
    public partial class PropertyContainer : VBoxContainer
    {
        public override partial void _Notification(int what);

        [Dependency]
        private List<Node> selected => DependOn<List<Node>>();

        private SpinBox layerBox;
        private ColorPicker colourPicker;

        public PropertyContainer ()
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

            setupProperties();
        }

        private void setupProperties()
        {
            AddChild(layerBox);
            AddChild(colourPicker);

            layerBox.ValueChanged += val =>
            {
                foreach (var selectableNode in selected.OfType<Node2D>())
                    selectableNode.ZIndex = (int)val;
            };

            colourPicker.ColorChanged += val =>
            {
                foreach (var selectableNode in selected.OfType<Node2D>())
                    selectableNode.SelfModulate = val;
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            Visible = selected.Any();
        }
    }
}
