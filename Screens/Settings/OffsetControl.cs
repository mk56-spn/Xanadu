// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.Settings
{
    public partial class OffsetControl : SettingsControl
    {
        private SpinBox box = new();
        public OffsetControl(string icon, string settingName) : base(icon, settingName)
        {
            AddChild(box);
            box.Value = GameSettings.CurrentSettings.OffSetMs;

            box.ValueChanged += v =>
            {
                GameSettings.CurrentSettings.OffSetMs = (int)v;
                GameSettings.SaveSettings();
                GD.Print(GameSettings.CurrentSettings.OffSetMs);
            };
        }
    }
}
