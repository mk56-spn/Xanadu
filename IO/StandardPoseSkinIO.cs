// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.IO.Indexes;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.Skeleton;
using static XanaduProject.IO.SerializationUtils;

namespace XanaduProject.IO
{
    public static class StandardPoseSkinIo
    {
        public static void Save(StandardPoseSkin skin, string fileName, bool rebuildIndex = true)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(skin, options);

            // Ensure the directory exists
            DirAccess.MakeDirRecursiveAbsolute(SKINS_DIR);
            string filePath = Path.Combine(SKINS_DIR, $"{fileName}.json");
            File.WriteAllText(ProjectSettings.GlobalizePath(filePath), json);

            if(rebuildIndex)
                // Rebuild the index to include the new skin
                SkinIndex.BuildIndex();
        }
    }
}
