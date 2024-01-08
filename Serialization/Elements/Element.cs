// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record Element
    {
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public float Skew;
        public int Group;

        public Transform2D GetElementTransform() =>
            new Transform2D(Mathf.DegToRad(Rotation), Scale, Skew, Position);

        public virtual Vector2 GetSize() =>
            new Vector2(100, 100);
    }
}
