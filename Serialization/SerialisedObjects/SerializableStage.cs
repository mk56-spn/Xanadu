// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Serialization.SerialisedObjects
{
    public class SerializableStage
    {
        public Element[] Elements = [];
        public Texture[] DynamicTextures = [];
    }
}
