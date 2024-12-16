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
        public static SerializableStage Deserialize()
        {

            EntitySerializer s = new EntitySerializer();
            string dir = $"{OS.GetUserDataDir()}/TestFile.json";

            SerializableStage serializableStage;
            if (File.Exists(dir))
            {
                try
                {
                    var serializer = new EntitySerializer();
                    var targetStore = new EntityStore();
                    serializer.ReadIntoStore(targetStore, new FileStream($"{OS.GetUserDataDir()}/TestFile.json", FileMode.Open));

                    Console.WriteLine($"entities: {targetStore.Count}"); // > entities: 2

                    serializableStage = new SerializableStage { EntityStore = targetStore };

                    GD.Print("successfully loaded file");
                }
                catch (Exception e)
                {
                    serializableStage = new TestSerializableStage();
                    Console.WriteLine(e);
                }
            }

            else
                serializableStage = new TestSerializableStage();

            return serializableStage;
        }
    }
}
