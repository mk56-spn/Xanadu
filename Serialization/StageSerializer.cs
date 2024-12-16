// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Ceras;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.Serialization.SerialisedObjects;

namespace XanaduProject.Serialization
{
    public static class StageSerializer
    {
        public static void Serialize(EntityStore store)
        {
            // --- Write store entities as JSON array
            var serializer = new EntitySerializer();
            var writeStream = new FileStream($"{OS.GetUserDataDir()}/TestFile.json", FileMode.Create);

            serializer.WriteStore(store, writeStream);
            writeStream.Close();
        }
    }
}
