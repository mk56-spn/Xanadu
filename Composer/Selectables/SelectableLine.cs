// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectableLine : Selectable
    {
        private readonly Line2D line;

        private readonly Line2D selectionLine = new Line2D { DefaultColor = Colors.Gold, Width = 2 } ;

        public SelectableLine (Line2D line)
        {
            this.line = line;

            Visible = false;
            SelectionStateChanged += b => Visible = b;

            int i = 0;
            foreach (var _ in line.Points)
            {
                AddChild(new LineHandle(line, i));
                i++;
            }

            AddChild(selectionLine);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (selectionLine.Points.Equals(line.Points)) return;

            selectionLine.Points = line.Points;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventMouseButton { Pressed: true } mouse) return;

            switch (mouse)
            {
                case { ButtonIndex: MouseButton.Left } :
                    Curve2D curve = new Curve2D();

                    foreach (var point in line.Points)
                        curve.AddPoint(point);

                    var mouseLocal = GetLocalMousePosition();

                    bool selected = curve.GetClosestPoint(mouseLocal).DistanceTo(mouseLocal) < line.Width / 2f;
                    Selected(selected);

                    curve.Dispose();
                    break;

                case { ButtonIndex: MouseButton.Right }:
                    line.AddPoint(ToLocal(GetTruePosition()));
                    AddChild(new LineHandle(line, line.Points.Length - 1) { Position = line.Points.Last() });
                    break;
            }
        }

        private partial class LineHandle : SelectableHandle
        {
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
