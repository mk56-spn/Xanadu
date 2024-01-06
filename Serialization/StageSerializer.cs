// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Ceras;
using Godot;
using XanaduProject.Serialization.SerialisedObjects;

namespace XanaduProject.Serialization
{
    public static class StageSerializer
    {
        public static void Serialize(SerializableStage serializableStage)
        {
            byte[] bytes = new CerasSerializer().Serialize(serializableStage);
            File.WriteAllBytes($"{OS.GetUserDataDir()}/TestFile.txt", bytes);
        }
    }
}
