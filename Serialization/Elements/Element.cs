// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public abstract record Element
    {
        public int Zindex { get; set; } = 1;
        public Color Colour { get; set; } = Colors.White;
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public int Group = 1;
        public Vector2 Scale;
        public float Skew;

        public Transform2D Transform => new(Mathf.DegToRad(Rotation), Scale, Skew, Position);

        public virtual Vector2 Size() => new(32, 32);

        public virtual Color ComposerColour => Colors.White;
    }
}
