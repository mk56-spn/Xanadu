// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record PhysicsElement : Element
    {
        public Vector2 Extents = Vector2.One;

        public override Vector2 Size() => Extents;
        public override Color ComposerColour => Colors.Green;
    }
}
