// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.Serialization
{
    public static class StageSerializer
    {
        public static void Serialize(EntityStore store, string filename)
        {
            shimCreation(store);
            // --- Write store entities as JSON array
            var serializer = new EntitySerializer();


            string path = ProjectSettings.GlobalizePath("res://Stages");

            using var writeStream = new FileStream($"{path}/{filename}.json", FileMode.Create);
            serializer.WriteStore(store, writeStream);
        }

        private static void shimCreation(EntityStore store)
        {
            var buffer = store.GetCommandBuffer();
            store.Query<ColorArrayEcs>().ForEachEntity((ref ColorArrayEcs component1, Entity entity) =>
                buffer.AddComponent(entity.Id, new ColorArrayThin(component1)));

            buffer.Playback();
        }
    }
}
