// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.ColourChannels;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.IO.Serialization
{
    public static class StageDeserializer
    {
        private static void shimConverter(EntityStore store)
        {
            var buffer = store.GetCommandBuffer();
            store.Query<ColorArrayThin>().ForEachEntity((ref ColorArrayThin component1, Entity entity) =>
            {
                buffer.AddComponent(entity.Id, new ColorArrayEcs(component1));
                buffer.AddTag<Dormant>(entity.Id);
                buffer.AddComponent<ActiveColourEcs>(entity.Id);
                buffer.RemoveComponent<ColorArrayThin>(entity.Id);
            });

            buffer.Playback();

            store.Query<ElementEcs>().ForEachEntity((ref ElementEcs component1, Entity entity) =>
            {
                component1.Canvas = RenderRid.Create();
            });
        }

        public static EntityStore Deserialize(string stageName)
        {

            string entityStorePath = Path.Combine(ProjectSettings.GlobalizePath(stageName), SerializationUtils.ENTITY_STORE_FILENAME);

            EntityStore serializableStage;

            if (File.Exists(entityStorePath))
            {
                try
                {
                    var serializer = new EntitySerializer();
                    var targetStore = new EntityStore
                    {
                        JobRunner = new ParallelJobRunner(16)
                    };
                    serializer.ReadIntoStore(targetStore, new FileStream(entityStorePath, FileMode.Open));

                    shimConverter(targetStore);

                    serializableStage = targetStore;

                    GD.PrintRich("[code][color=green] Successfully loaded file" + targetStore.Count);
                }
                catch (Exception e)
                {
                    GD.PrintErr("FAILURE");

                    serializableStage = CleanStage();
                    Console.WriteLine(e);
                }
            }
            else
            {
                GD.PrintErr("FAILURE, FILE NOT FOUND");
                serializableStage = CleanStage();
            }
            return serializableStage;
        }

        public static EntityStore CleanStage()
        {
            return new EntityStore();
        }
    }
}
