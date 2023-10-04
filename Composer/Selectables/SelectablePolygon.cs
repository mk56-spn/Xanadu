// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectablePolygon : Selectable
    {
        private readonly Polygon2D mainPolygon;
        protected override Color HighlightColor => Colors.Gold;

        private ConvexPolygonShape2D hitboxPolygon = new ConvexPolygonShape2D();
        private Line2D outline = new Line2D { Width = 2, Closed = true };

        public SelectablePolygon (Polygon2D mainPolygon)
        {
            this.mainPolygon = mainPolygon;
            AddChild(outline);

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

            ChildEnteredTree += node =>
            {
                if (node is not Node2D node2D) return;

                node2D.Visible = false;
                SelectionStateChanged += b =>
                {
                    QueueRedraw();
                    node2D.Visible = b;
                };
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            outline.DefaultColor = HighlightColor with { A = 0.5f + Mathf.Sin(Time.GetTicksMsec() / 300f) / 2f };

            if (!IsHeld) return;

            mainPolygon.GlobalPosition = GetTruePosition(true);
        }

        public override void _Draw()
        {
            base._Draw();

            if (!IsSelected) return;

            const int size = 10;

            DrawMultiline(new []
            {
                new Vector2(-size, 0),
                new Vector2(size, 0),
                new Vector2(0, -size),
                new Vector2(0, size)
            }, Colors.OrangeRed, 4);
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
