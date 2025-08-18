// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.Settings
{
    public partial class SettingsControl : HBoxContainer
    {
        public SettingsControl(string icon, string settingName)
        {
            AddChild(new Label { Text = icon});
            AddChild(new Label { Text = settingName });
        }
    }
}
