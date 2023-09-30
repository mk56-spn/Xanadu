// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Composable
{
    // Base type for all polygons used in the project.
    public partial class XanaduPolygon : Polygon2D
    {
        protected XanaduPolygon ()
        {
            if (Polygon.Length != 0) return;
                // We want to assign a default shape to polygons to
                // make sure they are instantiated in an invisible shape.
            Polygon = new []
            {
                new Vector2(-30, -30),
                new Vector2(30, -30),
                new Vector2(30, 30),
                new Vector2(-30, 30)
            };
        }
    }
}
