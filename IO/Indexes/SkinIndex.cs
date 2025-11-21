// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using MemoryPack;
using XanaduProject.Screens.AssetCreation.Skeleton;
using static XanaduProject.IO.SerializationUtils;

namespace XanaduProject.IO.Indexes
{
    public abstract class SkinIndex
    {
        private static readonly Dictionary<string, StandardPoseSkin> skins = new();

        static SkinIndex() => BuildIndex();

        public static void BuildIndex()
        {
            skins.Clear();
            string skinsDir = ProjectSettings.GlobalizePath(SKINS_DIR);

            // Ensure the directory exists
            DirAccess.MakeDirRecursiveAbsolute(skinsDir);

            foreach (string filePath in Directory.EnumerateFiles(skinsDir, $"*{SKIN_EXT}"))
            {
                try
                {
                    byte[] v = File.ReadAllBytes(filePath);
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    StandardPoseSkin newSkin = MemoryPackSerializer.Deserialize<StandardPoseSkin>(v) ?? throw new InvalidOperationException();

                    skins.Add(fileName, newSkin);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"SkinIndex: An unexpected error occurred while processing '{filePath}': {e.Message}");
                }
            }

            // If no skins were found, create and save a default one.
            if (skins.Count != 0) return;
            GD.Print("SkinIndex: No skins found, creating a default skin.");
            var defaultSkin = new StandardPoseSkin(SkeletonBuilder.GetBoneNames());
            StandardPoseSkinIo.Save(defaultSkin, rebuildIndex: true);
            skins.Add("default", defaultSkin);
        }

        public static IReadOnlyDictionary<string, StandardPoseSkin> GetAllSkins()
        {
            return skins;
        }
    }
}
