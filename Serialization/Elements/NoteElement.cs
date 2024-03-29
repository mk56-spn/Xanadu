// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record NoteElement : Element
    {
        public const float RADIUS = 30;
        public float TimingPoint;

        public override Color ComposerColour => Colors.Magenta;
    }
}
