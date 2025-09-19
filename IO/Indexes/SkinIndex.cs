// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.Scenes;
using XanaduProject.Scenes.MeshEditor;
using static XanaduProject.IO.SerializationUtils;

namespace XanaduProject.IO.Indexes
{
    public static class SkinIndex
    {
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
                    var boneNames = JsonSerializer.Deserialize<List<string>>(json);

                    if (boneNames == null)
                    {
                        GD.PrintErr($"SkinIndex: Failed to deserialize skin from '{filePath}'. Skipping.");
                        continue;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    var skin = new StandardPoseSkin(boneNames);

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
