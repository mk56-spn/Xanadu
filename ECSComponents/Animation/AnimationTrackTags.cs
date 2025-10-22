// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using System;
using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.Animation
{
    public struct AddKeyFrame(float position, ValueType? value = null) : IComponent
    {
        public readonly float Position = position;
        public ValueType? CustomValue = value;
    }
    public struct RemoveKeyFrame() : IComponent
    {
        public int Index = 0;
    }

    public struct UpdateKeyPosition(float time, int index) : IComponent
    {
        public int Index = index;
        public float Time = time;
    }

    public struct KeyFrameColor
    {
        public Color Color;
    }

    public struct KeyFrameVector
    {
        public Vector2 Vector;
    }
    public struct KeyFrameAngle
    {
        public float Angle;
    }
}
