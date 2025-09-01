// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.IO.Indexes
{
    public static class TrackIndex
    {
        private static Dictionary<int, TrackInfo> tracks { get; } = new();

        static TrackIndex()
            => buildIndex();

        private static void buildIndex()
        {
            tracks.Clear();
            const string tracks_res_dir = "res://Tracks/";
            string? tracksDir = ProjectSettings.GlobalizePath(tracks_res_dir);

            if (!Directory.Exists(tracksDir))
            {
                GD.PrintErr($"TrackIndex: No tracks directory found at '{tracksDir}'.");
                return;
            }

            foreach (string dirPath in Directory.EnumerateDirectories(tracksDir))
            {
                string dirName = new DirectoryInfo(dirPath).Name;
                if (!int.TryParse(dirName, out int trackIndex)) continue;

                string metadataPath = Path.Combine(dirPath, "metadata.json");
                if (!File.Exists(metadataPath)) continue;

                string? songPath = Directory.EnumerateFiles(dirPath)
                    .FirstOrDefault(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));

                if (songPath == null)
                {
                    GD.PrintErr($"TrackIndex: No .ogg file found in track directory '{dirPath}'. Skipping track.");
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(metadataPath);
                    var trackInfo = JsonSerializer.Deserialize<TrackInfo?>(json);
                    if (trackInfo == null) continue;

                    string songResPath = tracks_res_dir.PathJoin(dirName).PathJoin(Path.GetFileName(songPath));
                    var newTrackInfo = trackInfo.Value with { Track = songResPath };
                    tracks.Add(trackIndex, newTrackInfo);
                }
                catch (JsonException e)
                {
                    GD.PrintErr($"TrackIndex: Failed to parse JSON from '{metadataPath}': {e.Message}");
                }
                catch (IOException e)
                {
                    GD.PrintErr($"TrackIndex: Failed to read file '{metadataPath}': {e.Message}");
                }
            }
        }

        public static TrackInfo GetTrackInfo(int index)
        {
            tracks.TryGetValue(index, out var trackInfo);
            return trackInfo;
        }
    }
}
