// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;
using Godot;

namespace XanaduProject.Serialization
{

    [StructLayout(LayoutKind.Explicit)]
    public struct ColorThin(float r, float g, float b, float a)
    {
        [FieldOffset(0)]
        public float R = r;
        [FieldOffset(4)]
        public float G = g;
        [FieldOffset(8)]
        public float B = b;

        [FieldOffset(12)]
        public float A = a;

        public static implicit operator ColorThin(Color color)=> new(color.R, color.G, color.B,color.A);
        public static implicit operator Color(ColorThin color)=> new(color.R, color.G, color.B,color.A);
    }
}
