using System.Text.Json;
using Godot;

namespace XanaduProject
{
    public static class GameSettings
    {
        public class SettingsData
        {
            public int ResolutionWidth { get; set; } = 1920;
            public int ResolutionHeight { get; set; } = 1080;

            public int OffSetMs { get; set; }
        }

        public static SettingsData CurrentSettings { get; private set; } = new();

        private static readonly string settings_file_path = "user://settings.json";

        public static void LoadSettings()
        {
            if (FileAccess.FileExists(settings_file_path))
            {
                using var file = FileAccess.Open(settings_file_path, FileAccess.ModeFlags.Read);
                string content = file.GetAsText();
                if (!string.IsNullOrEmpty(content))
                {
                    try
                    {
                        CurrentSettings = JsonSerializer.Deserialize<SettingsData>(content)!;
                    }
                    catch (JsonException e)
                    {
                        GD.PrintErr($"Error loading settings: {e.Message}");
                        // Handle error, maybe by loading default settings
                        CurrentSettings = new SettingsData();
                        SaveSettings(); // Save the default settings
                    }
                }
            }
            else
            {
                // If the settings file doesn't exist, create it with default values.
                SaveSettings();
            }
        }

        public static void SaveSettings()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string content = JsonSerializer.Serialize(CurrentSettings, options);
            using var file = FileAccess.Open(settings_file_path, FileAccess.ModeFlags.Write);
            file.StoreString(content);
        }

        public static void ApplyResolution()
        {
            // If the window is maximized, restore it to a normal state before resizing
            if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Maximized)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            }

            var newSize = new Vector2I(CurrentSettings.ResolutionWidth, CurrentSettings.ResolutionHeight);
            DisplayServer.WindowSetSize(newSize);

            // Center the window on the screen to ensure content updates
            var screenSize = DisplayServer.ScreenGetSize();
            var windowPosition = (screenSize - newSize) / 2;
            DisplayServer.WindowSetPosition(windowPosition);
        }
    }
}
