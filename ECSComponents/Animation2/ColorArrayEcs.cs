// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using System;
using System.Collections;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Serialization;
using ZLinq;

namespace XanaduProject.ECSComponents.Animation2
{
    [ComponentKey(null)]
    public struct ColorArrayEcs : IComponent
    {
        public Color[] Colors = [];
        public ColorArrayEcs(ColorArrayThin arrayThin)
        {
            Colors = arrayThin.Colors.AsValueEnumerable().Select(c=> (Color)c).ToArray();
        }
    }
}
