// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.Settings
{
    public partial class WindowSettingsControl : HBoxContainer
    {
        private OptionButton resolutionDropdown;

        public WindowSettingsControl()
        {
            // Add label
            AddChild(new Label { Text = "Resolution" });

            // Add dropdown
            resolutionDropdown = new OptionButton();
            AddChild(resolutionDropdown);

            populateResolutions();
            selectCurrentResolution();

            resolutionDropdown.ItemSelected += OnResolutionSelected;
        }

        private void populateResolutions()
        {
            resolutionDropdown.AddItem("1920x1080");
            resolutionDropdown.AddItem("1280x720");
            resolutionDropdown.AddItem("800x600");
        }

        private void selectCurrentResolution()
        {
            string currentResolution = $"{GameSettings.CurrentSettings.ResolutionWidth}x{GameSettings.CurrentSettings.ResolutionHeight}";
            for (int i = 0; i < resolutionDropdown.ItemCount; i++)
            {
                if (resolutionDropdown.GetItemText(i) != currentResolution) continue;

                resolutionDropdown.Select(i);
                break;
            }
        }

        private void OnResolutionSelected(long index)
        {
            string selectedResolution = resolutionDropdown.GetItemText((int)index);
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
