// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectablePolygon : Selectable
    {
        protected override Color HighlightColor => Colors.Blue;

        private ConvexPolygonShape2D hitboxPolygon = new ConvexPolygonShape2D();

        public SelectablePolygon (Polygon2D mainPolygon)
        {
            CollisionShape.Shape = hitboxPolygon;

            mainPolygon.Draw += () =>
            {
                hitboxPolygon.Points = mainPolygon.Polygon;
                QueueRedraw();
            };

            int i = 0;
            foreach (var _ in mainPolygon.Polygon)
            {
                AddChild(new PolygonHandle(mainPolygon, i));
                i++;
            }
        }

        public override void _Draw()
        {
            base._Draw();
            DrawColoredPolygon(hitboxPolygon.Points, Colors.White);
        }

        private partial class PolygonHandle : SelectableHandle
        {
            protected override Color HighlightColor { get; } = Colors.Red;

            public PolygonHandle (Polygon2D polygon, int index)
            {
                Position = polygon.Polygon[index];
                Radius = 10;

                OnPositionChanged += () =>
                {
                    var a = polygon.Polygon;
                    a.SetValue((GlobalPosition - polygon.Position) / polygon.Scale, index);
                    polygon.Polygon = a;
                };
            }

            public override void _Draw()
            {
                base._Draw();
                DrawCircle(Vector2.Zero, 10, Colors.Snow);
            }
        }
    }
}
