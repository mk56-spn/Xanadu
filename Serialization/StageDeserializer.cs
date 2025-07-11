// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Tools;
using static Godot.Colors;
using static XanaduProject.Tools.EasingType;

namespace XanaduProject.Serialization
{
    public static class StageDeserializer
    {
        private static void shimConverter(EntityStore store)
        {
            var buffer = store.GetCommandBuffer();
            store.Query<ColorArrayThin>().ForEachEntity((ref ColorArrayThin component1, Entity entity) =>
            {
                buffer.AddComponent(entity.Id, new ColorArrayEcs(component1));
                buffer.RemoveComponent<ColorArrayThin>(entity.Id);
            });

            buffer.Playback();
        }
        public static SerializableStage Deserialize(string filename)
        {
            string path = ProjectSettings.GlobalizePath("res://Stages");

            string dir = $"{path}/{filename}.json";

            SerializableStage serializableStage;
            if (File.Exists(dir))
                try
                {
                    var serializer = new EntitySerializer();
                    var targetStore = new EntityStore
                    {
                        JobRunner = new ParallelJobRunner(16)
                    };
                    serializer.ReadIntoStore(targetStore, new FileStream(dir, FileMode.Open));

                    shimConverter(targetStore);


                    serializableStage = new SerializableStage { EntityStore = targetStore };

                    GD.PrintRich("[code][color=green] Successfully loaded file" + targetStore.Count);

                    var v = targetStore.GetCommandBuffer();


                }
                catch (Exception e)
                {
                    GD.PrintErr("FAILURE");

                    serializableStage = new SerializableStage
                    {
                        EntityStore = new EntityStore()
                    };
                    Console.WriteLine(e);
                }

            else
            {
                GD.PrintErr("FAILURE, FILE NOT FOUND");

                serializableStage = new SerializableStage
                {
                    EntityStore = new EntityStore()
                };


                for (int i = 0; i < 100; i++)
                {
                    serializableStage.EntityStore.CreateEntity(
                        new FloatArrayEcs
                        {
                            Points = [],
                            Easing = [],
                        },
                        new ColorArrayEcs
                        {
                            Colors = []
                        }
                    );
                }
            }


            return serializableStage;
        }
    }
}
