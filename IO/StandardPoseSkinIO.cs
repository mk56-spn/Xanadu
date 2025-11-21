// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Godot;
using MemoryPack;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.AssetCreation.Skeleton;
using static XanaduProject.IO.SerializationUtils;

namespace XanaduProject.IO
{
    public static class StandardPoseSkinIo
    {
        public static void Save(StandardPoseSkin skin, bool rebuildIndex = true)
        {
            byte[] v = MemoryPackSerializer.Serialize(skin);

            // Ensure the directory exists
            DirAccess.MakeDirRecursiveAbsolute(SKINS_DIR);
            string filePath = Path.Combine(SKINS_DIR, $"{skin.Name}{SKIN_EXT}");
            File.WriteAllBytes(ProjectSettings.GlobalizePath(filePath), v);

            if(rebuildIndex)
                // Rebuild the index to include the new skin
                SkinIndex.BuildIndex();
        }
    }
}
