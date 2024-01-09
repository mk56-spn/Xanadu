// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record TextureElement : Element
    {
        public Vector2 Extents { get; } = new Vector2(64, 64);

        public override Vector2 Size() => Extents;
    }
}
