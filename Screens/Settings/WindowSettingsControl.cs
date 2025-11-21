// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.Buttons;
using XanaduProject.UiElements;

namespace XanaduProject.Screens.Settings
{
    public partial class WindowSettingsControl : SettingsControl
    {
        private readonly CustomAnimatedDropdown resolutionDropdown;
        private readonly List<string> resolutions = ["1920x1080", "1280x720", "800x600"];

        public WindowSettingsControl(string icon, string settingName) : base(icon, settingName)
        {
            string currentResolution = $"{GameSettings.CurrentSettings.ResolutionWidth}x{GameSettings.CurrentSettings.ResolutionHeight}";

            string placeholder = "Select Resolution";
            if (resolutions.Contains(currentResolution))
            {
                placeholder = currentResolution;
            }

            resolutionDropdown = new CustomAnimatedDropdown(placeholder, resolutions, 30);
            AddChild(resolutionDropdown);

            resolutionDropdown.ItemSelected += OnResolutionSelected;
        }

        private void OnResolutionSelected(int index)
        {
            string selectedResolution = resolutions[index];
            string[] parts = selectedResolution.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) ||
                !int.TryParse(parts[1], out int height)) return;

            GameSettings.CurrentSettings.ResolutionWidth = width;
            GameSettings.CurrentSettings.ResolutionHeight = height;
            GameSettings.SaveSettings();
            GameSettings.ApplyResolution();
        }
    }
}
