// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents.Animation2
{
    public struct FloatArrayEcs(int value) : IComponent
    {
        public float[] Points = [];
        public EasingType[] Easing = [];

        public int Value = value;

        public int GetIndexedValue()
        {
            return Value;
        }
    }
}
