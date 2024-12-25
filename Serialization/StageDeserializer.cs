// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Tests;

namespace XanaduProject.Serialization
{
    public abstract class StageDeserializer
    {
        public static SerializableStage Deserialize(string filename)
        {

            string path = ProjectSettings.GlobalizePath("res://Stages");

            string dir = $"{path}/{filename}.json";

            SerializableStage serializableStage;
            if (File.Exists(dir))
            {
                try
                {
                    var serializer = new EntitySerializer();
                    var targetStore = new EntityStore();
                    serializer.ReadIntoStore(targetStore, new FileStream(dir, FileMode.Open));

                    serializableStage = new SerializableStage { EntityStore = targetStore };

                    GD.PrintRich("[code][color=green] Successfully loaded file");
                }
                catch (Exception e)
                {
                    serializableStage = new SerializableStage()
                    {
                        EntityStore = new EntityStore(),
                    };
                    Console.WriteLine(e);
                }
            }

            else
                serializableStage = new SerializableStage()
                {
                    EntityStore = new EntityStore(),
                };

            return serializableStage;
        }
    }
}
