// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectableLine : Selectable
    {
        private readonly Line2D line;
        private Path2D selectionPath = new Path2D { Curve = new Curve2D() };

        public SelectableLine (Line2D line)
        {
            this.line = line;
            AddChild(selectionPath);

            int i = 0;
            foreach (var _ in line.Points)
            {
                AddChild(new LineHandle(line, i));
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

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;

            selectionPath.Curve.ClearPoints();

            foreach (var point in line.Points)
                selectionPath.Curve.AddPoint(point);

            if (!(selectionPath.Curve.GetClosestPoint(GetLocalMousePosition())
                    .DistanceTo(GetLocalMousePosition()) < line.Width / 2))
            {
                Selected(false);
                return;
            }

            if (IsSelected)
            {
                line.AddPoint(new Vector2(50, 50));
                AddChild(new LineHandle(line, line.Points.Length - 1));
                return;
            }

            Selected(true);
        }

        private partial class LineHandle : SelectableHandle
        {
            protected override Color HighlightColor { get; } = Colors.Red;

            public LineHandle(Line2D line2D, int index)
            {
                MoveOnDrag = true;
                Position = line2D.Points[index];
                Radius = 10;

                OnDragged += () =>
                {
                    var a = line2D.Points;
                    a.SetValue(line2D.ToLocal(GetTruePosition()), index);
                    line2D.Points = a;
                };
            }
        }
    }
}
