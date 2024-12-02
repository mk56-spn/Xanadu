// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record TextureElement : Element
    {
        private Vector2 extents { get; } = new(64, 64);
        public int Texture;

        public override Vector2 Size() => extents;
    }
}
