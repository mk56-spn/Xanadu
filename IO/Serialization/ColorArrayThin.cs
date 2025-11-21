// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.Animation2;
using ZLinq;

namespace XanaduProject.IO.Serialization
{
    public struct ColorArrayThin : IComponent
    {
        public readonly ColorThin[] Colors = [];

        public ColorArrayThin(ColorArrayEcs color)
        {
            Colors = color.Points.AsValueEnumerable().Select(c => (ColorThin)c).ToArray();
        }
    }
}
