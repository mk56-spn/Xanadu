// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Ceras;
using Godot;
using XanaduProject.Serialization.SerialisedObjects;

namespace XanaduProject.Serialization
{
    public abstract class StageDeserializer
    {
        public static SerializableStage Deserialize()
        {
            byte[] readBytes = File.ReadAllBytes($"{OS.GetUserDataDir()}/TestFile.txt");

            SerializableStage serializableStage = new CerasSerializer().Deserialize<SerializableStage>(readBytes);

            return serializableStage;
        }
    }
}
