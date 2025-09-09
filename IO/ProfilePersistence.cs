// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.IO
{
    public static class ProfilePersistence
    {
        public static void SaveProfile(ProfileInfo profileInfo)
        {
            string profileDir = ProjectSettings.GlobalizePath(profileInfo.ProfilePath);

            if (!Directory.Exists(profileDir))
            {
                try
                {
                    Directory.CreateDirectory(profileDir);
                }
                catch (IOException e)
                {
                    GD.PrintErr($"ProfilePersistence: Failed to create directory '{profileDir}': {e.Message}");
                    return;
                }
            }

            string profileJsonPath = Path.Combine(profileDir, SerializationUtils.PROFILE_FILENAME);

            try
            {
                string json = JsonSerializer.Serialize(profileInfo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profileJsonPath, json);
            }
            catch (JsonException e)
            {
                GD.PrintErr($"ProfilePersistence: Failed to serialize profile to JSON: {e.Message}");
            }
            catch (IOException e)
            {
                GD.PrintErr($"ProfilePersistence: Failed to write to file '{profileJsonPath}': {e.Message}");
            }
        }
    }
}
