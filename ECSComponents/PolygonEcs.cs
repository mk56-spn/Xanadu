// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents
{
    public struct PolygonEcs : IComponent
    {
        public Vector2[] Points = default_points;

        public PolygonEcs()
        {
        }

        private static readonly Vector2[] default_points =
        [
            new(-50, -50),
            new(0, -50),
            new(-50, 50)
        ];


        [UsedImplicitly]
        public static void CopyValue(in PolygonEcs source, ref PolygonEcs target, in CopyContext context)
        {
            GD.Print("called poly");
            // Perform a deep copy of the Points array
            target.Points = new Vector2[source.Points.Length];
            Array.Copy(source.Points, target.Points, source.Points.Length);
        }
    }
}
