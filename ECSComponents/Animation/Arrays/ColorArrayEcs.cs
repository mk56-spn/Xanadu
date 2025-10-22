// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.Engine.ECS;
using Godot;
using XanaduProject.IO.Serialization;
using ZLinq;

namespace XanaduProject.ECSComponents.Animation.Arrays
{
    [ComponentKey(null)]
    public struct ColorArrayEcs : IComponent, IEcsArray<Color>
    {
        public ColorArrayEcs(ColorArrayThin arrayThin)
        {
            Points = arrayThin.Colors.AsValueEnumerable().Select(c => (Color)c).ToArray();
        }

        public Color[] Points { get; set; }
    }
}
