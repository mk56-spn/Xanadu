// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectablePolygon : Selectable
    {
        protected override Color HighlightColor => Colors.Gold;

        private ConvexPolygonShape2D hitboxPolygon = new ConvexPolygonShape2D();
        private Line2D outline = new Line2D { Width = 2, Closed = true };

        public SelectablePolygon (Polygon2D mainPolygon)
        {
            AddChild(outline);

            Visible = false;
            CollisionShape.Shape = hitboxPolygon;

            mainPolygon.Draw += () =>
            {
                hitboxPolygon.Points = mainPolygon.Polygon;
                outline.Points = mainPolygon.Polygon;
            };

            int i = 0;
            foreach (var _ in mainPolygon.Polygon)
            {
                AddChild(new PolygonHandle(mainPolygon, i));
                i++;
            }

            SelectionStateChanged += state => Visible = state;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            base._Process(delta);

            outline.DefaultColor = HighlightColor with { A = 0.5f + Mathf.Sin(Time.GetTicksMsec() / 300f) / 2f };
        }

        private partial class PolygonHandle : SelectableHandle
        {
            public PolygonHandle (Polygon2D polygon, int index)
            {
                MoveOnDrag = true;
                Position = polygon.Polygon[index];
                Radius = 10;

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
