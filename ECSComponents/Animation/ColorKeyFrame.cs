// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.ECSComponents.Animation
{
    public record struct ColorKeyFrame( float Time, Color Color)
    {
        public readonly Color Color = Color;
        public readonly float Time = Time;
    }
}
