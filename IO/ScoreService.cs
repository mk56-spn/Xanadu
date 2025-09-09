// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.IO
{
    public static class ScoreService
    {
        private const string scores_dir_name = "scores";

        public static void SaveScore(Score score)
        {
            if (GameSettings.CurrentProfile == null) return;

            string profilePath = ProjectSettings.GlobalizePath(GameSettings.CurrentProfile.ProfilePath);
            string scoresDir = Path.Combine(profilePath, scores_dir_name);

            try
            {
                Directory.CreateDirectory(scoresDir);
            }
            catch (IOException e)
            {
                GD.PrintErr($"ScoreService: Could not create directory '{scoresDir}': {e.Message}");
                return;
            }

            string filename = $"{score.SongIndex}-{score.Timestamp:yyyyMMddHHmmss}.json";
            string scorePath = Path.Combine(scoresDir, filename);

            try
            {
                string json = JsonSerializer.Serialize(score, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(scorePath, json);
            }
            catch (IOException e)
            {
                GD.PrintErr($"ScoreService: Failed to write to file '{scorePath}': {e.Message}");
            }
        }

        public static List<Score> GetScores(string profilePath)
        {
            string scoresDir = Path.Combine(ProjectSettings.GlobalizePath(profilePath), scores_dir_name);
            var scores = new List<Score>();

            if (!Directory.Exists(scoresDir))
            {
                return scores;
            }

            foreach (string filePath in Directory.EnumerateFiles(scoresDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var score = JsonSerializer.Deserialize<Score>(json);
                    if (score != null)
                    {
                        scores.Add(score);
                    }
                }
                catch (JsonException e)
                {
                    GD.PrintErr($"ScoreService: Failed to parse JSON from '{filePath}': {e.Message}");
                }
                catch (IOException e)
                {
                    GD.PrintErr($"ScoreService: Failed to read file '{filePath}': {e.Message}");
                }
            }

            return scores;
        }
    }
}
