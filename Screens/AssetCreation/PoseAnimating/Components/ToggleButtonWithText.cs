// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Components
{
    public partial class ToggleButtonWithText : HBoxContainer
    {
        public event BaseButton.ToggledEventHandler Toggled
        {
            add => toggleButton.Toggled += value;
            remove => toggleButton.Toggled -= value;
        }

        private readonly AnimatedToggleButton toggleButton;

        public ToggleButtonWithText(string text)
        {
            var label = new Label() { Text = text, VerticalAlignment = VerticalAlignment.Center };
            toggleButton = new AnimatedToggleButton();

            AddChild(label);
            AddChild(toggleButton);
        }
    }
}
