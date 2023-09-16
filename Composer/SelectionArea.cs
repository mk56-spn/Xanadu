// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer
{
    [GlobalClass]
    public partial class SelectionArea : Control
    {
        private bool updateSelected;

        private bool dragging;
        private Vector2 dragStart;

        public override void _Draw()
        {
            base._Draw();

            if (!dragging) return;

            Vector2 size = GetLocalMousePosition() - dragStart;
            DrawRect(new Rect2(dragStart, size), Colors.Aqua, false, 2);
            DrawRect(new Rect2(dragStart, size), Colors.Aqua with { A = 0.3f });

            DrawString(new FontFile(), new Vector2(10, 20), dragging.ToString().ToUpper(), modulate: Colors.GreenYellow);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);
            QueueRedraw();

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;

            dragging = mouse.Pressed;

            if (mouse.Pressed)
            {
                dragStart =  GetLocalMousePosition();
                return;
            }

            updateSelected = true;
            GetViewport().SetInputAsHandled();
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!updateSelected) return;

            updateSelected = false;
            selectItems();
        }

        private void selectItems()
        {
            var dragEnd = GetLocalMousePosition();

            // We make sure to transform the rectangle so that its never negative
            Vector2 rectSize = new Vector2(Mathf.Abs(dragEnd.X - dragStart.X), Mathf.Abs(dragEnd.Y - dragStart.Y));

            // After the transform we check to see if the position of the rectangle needs changing.
            Vector2 rectanglePosition = new Vector2(Mathf.Min(dragStart.X, dragEnd.X), Mathf.Min(dragStart.Y, dragEnd.Y));

            var query = new PhysicsShapeQueryParameters2D
            {
                Transform =  new Transform2D(0,  rectanglePosition + rectSize / 2),
                Shape = new RectangleShape2D { Size = rectSize },
                CollideWithAreas = true,
                CollideWithBodies = false
            };

            GetWorld2D().DirectSpaceState.IntersectShape(query)
                .SelectMany(d => d.Values )
                .Select(v => v.Obj)
                .OfType<Selectable>()
                .ToList()
                .ForEach(s => s.Selected(true));
        }
    }
}
