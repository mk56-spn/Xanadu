// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectionPolygon : Selection
    {
        protected override Color HighlightColor => Colors.Blue;

        private ConvexPolygonShape2D hitboxPolygon = new ConvexPolygonShape2D();

        public SelectionPolygon (Polygon2D mainPolygon)
        {
            CollisionShape.Shape = hitboxPolygon;

            mainPolygon.Draw += () =>
            {
                hitboxPolygon.Points = mainPolygon.Polygon;
                QueueRedraw();
            };
        }

        public override void _Draw()
        {
            base._Draw();
            DrawColoredPolygon(hitboxPolygon.Points, Colors.White);
        }
    }
}
