// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectablePolygon : Selectable
    {
        protected override Color HighlightColor => Colors.Transparent;

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

            SelectionStateChanged += state =>
            {
                GetChildren()
                    .OfType<SelectableHandle>()
                    .ToList()
                    .ForEach(h => h.Visible = state);
            };
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
                MoveOnDrag = true;
                Position = polygon.Polygon[index];
                Radius = 10;
                Visible = false;

                OnDragged += () =>
                {
                    var a = polygon.Polygon;
                    a.SetValue(polygon.ToLocal(GetTruePosition()), index);
                    polygon.Polygon = a;
                };
            }
        }
    }
}
