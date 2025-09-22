// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using XanaduProject.Scenes;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.Skeleton;
using static XanaduProject.IO.SerializationUtils;

namespace XanaduProject.IO.Indexes
{
    public static class SkinIndex
    {
        // Helper record for migrating from the old format.
        private record OldStandardPoseSkin_Migration(List<Item> Bones);

        private static readonly Dictionary<string, StandardPoseSkin> Skins = new();

        static SkinIndex() => BuildIndex();

        public static void BuildIndex()
        {
            Skins.Clear();
            string skinsDir = ProjectSettings.GlobalizePath(SKINS_DIR);

            // Ensure the directory exists
            DirAccess.MakeDirRecursiveAbsolute(skinsDir);

            foreach (string filePath in Directory.EnumerateFiles(skinsDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Use JsonDocument to inspect the file for the old format.
                    using var doc = JsonDocument.Parse(json);

                    StandardPoseSkin skin;

                    if (doc.RootElement.TryGetProperty("Bones", out _))
                    {
                        // Old format detected, deserialize to the migration record.
                        var oldSkin = JsonSerializer.Deserialize<OldStandardPoseSkin_Migration>(json);
                        if (oldSkin == null)
                        {
                            GD.PrintErr($"SkinIndex: Failed to deserialize old format skin from '{filePath}'. Skipping.");
                            continue;
                        }

                        // Convert the old format to the new one.
                        var boneNames = oldSkin.Bones.Select(b => b.Name).ToList();
                        var itemAssignments = oldSkin.Bones
                            .Where(b => b.Components.Count > 0) // Only include items that actually have meshes
                            .ToDictionary(b => b.Name, b => b);

                        // We don't have Name, Description, Author from the old format, so use defaults.
                        skin = new StandardPoseSkin(
                            "Standard Skeleton",
                            "A standard pose for a mesh.",
                            "mk56_spn",
                            boneNames,
                            itemAssignments
                        );
                    }
                    else
                    {
                        // New format, deserialize directly.
                        var newSkin = JsonSerializer.Deserialize<StandardPoseSkin>(json);
                        if (newSkin == null)
                        {
                            GD.PrintErr($"SkinIndex: Failed to deserialize skin from '{filePath}'. Skipping.");
                            continue;
                        }
                        skin = newSkin;
                    }

                    Skins.Add(fileName, skin);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"SkinIndex: An unexpected error occurred while processing '{filePath}': {e.Message}");
                }
            }

            // If no skins were found, create and save a default one.
            if (Skins.Count == 0)
            {
                GD.Print("SkinIndex: No skins found, creating a default skin.");
                var defaultSkin = new StandardPoseSkin(SkeletonBuilder.BuildPoseAndGetBoneNames());
                StandardPoseSkinIo.Save(defaultSkin, "default", rebuildIndex: false);
                Skins.Add("default", defaultSkin);
            }
        }

        public static IReadOnlyDictionary<string, StandardPoseSkin> GetAllSkins()
        {
            return Skins;
        }
    }
}
