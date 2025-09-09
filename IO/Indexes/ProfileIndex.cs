// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.IO;

namespace XanaduProject.IO.Indexes
{
    public static class ProfileIndex
    {
        public static Dictionary<int, ProfileInfo> Profiles { get; } = new();

        static ProfileIndex()
        {
            BuildIndex();
        }

        public static void BuildIndex()
        {
            Profiles.Clear();
            string profilesDir = ProjectSettings.GlobalizePath(SerializationUtils.PROFILES_DIR);

            if (!Directory.Exists(profilesDir))
            {
                try
                {
                    Directory.CreateDirectory(profilesDir);
                }
                catch (IOException e)
                {
                    GD.PrintErr($"ProfileIndex: Could not create directory '{profilesDir}': {e.Message}");
                    return;
                }
            }

            int profileIndex = 0;
            foreach (string dirPath in Directory.EnumerateDirectories(profilesDir))
            {
                string profileJsonPath = Path.Combine(dirPath, SerializationUtils.PROFILE_FILENAME);

                if (!File.Exists(profileJsonPath))
                {
                    GD.PrintErr($"ProfileIndex: Missing profile.json in '{dirPath}'. Skipping profile.");
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(profileJsonPath);
                    var profileInfo = JsonSerializer.Deserialize<ProfileInfo>(json);

                    string dirName = new DirectoryInfo(dirPath).Name;
                    profileInfo.ProfilePath = SerializationUtils.PROFILES_DIR.PathJoin(dirName);

                    profileInfo.Scores = ScoreService.GetScores(profileInfo.ProfilePath);

                    Profiles.Add(profileIndex, profileInfo);
                    profileIndex++;
                }
                catch (JsonException e)
                {
                    GD.PrintErr($"ProfileIndex: Failed to parse JSON from '{profileJsonPath}': {e.Message}");
                }
                catch (IOException e)
                {
                    GD.PrintErr($"ProfileIndex: Failed to read file '{profileJsonPath}': {e.Message}");
                }
            }
        }
    }
}
