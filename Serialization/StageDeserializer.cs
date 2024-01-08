// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Ceras;
using Godot;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Tests;

namespace XanaduProject.Serialization
{
    public abstract class StageDeserializer
    {
        public static SerializableStage Deserialize()
        {
            string dir = $"{OS.GetUserDataDir()}/TestFile.txt";

            SerializableStage serializableStage;
            if (File.Exists(dir))
            {
                try
                {
                    byte[] readBytes = File.ReadAllBytes(dir);
                    serializableStage = new CerasSerializer().Deserialize<SerializableStage>(readBytes);
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
