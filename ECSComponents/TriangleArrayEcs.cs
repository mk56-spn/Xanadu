// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;

namespace XanaduProject.ECSComponents
{
    public struct TriangleArrayEcs() : IComponent
    {
        public float[] Points = [];

        [UsedImplicitly]
        public static void CopyValue(in TriangleArrayEcs source, ref TriangleArrayEcs target, in CopyContext context)
        {
            /*GD.Print("called poly");
            // Perform a deep copy of the Points array
            target.Points = new Vector2[source.Points.Length];
            Array.Copy(source.Points, target.Points, source.Points.Length);*/
            target.Points = [];
        }
    }
}

