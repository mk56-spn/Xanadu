// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer
{
    [GlobalClass]
    [SuperNode(typeof(Dependent))]
    public partial class SelectionArea : Control
    {
        public override partial void _Notification(int what);

        private bool updateSelected;

        private bool dragging;
        private Vector2 dragStart;

        private RectangleShape2D selectionRect = new RectangleShape2D();

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

            if (Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.Shift)) return;

            QueueRedraw();

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;

            dragging = mouse.Pressed;

            if (mouse.Pressed)
            {
                dragStart =  GetLocalMousePosition();
                return;
            }

            updateSelected = true;
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
            var space = GetWorld2D().DirectSpaceState;
            var query = new PhysicsShapeQueryParameters2D();

            var dragEnd = GetLocalMousePosition();

            if (dragEnd.X < dragStart.X)
                (dragEnd.X, dragStart.X) = (dragStart.X, dragEnd.X);

            if (dragEnd.Y < dragStart.Y)
                (dragEnd.Y, dragStart.Y) = (dragStart.Y, dragEnd.Y);

            selectionRect.Size = dragEnd - dragStart;

            Vector2 areaPosition = GetViewport().GetCamera2D().Offset + dragStart + (dragEnd - dragStart) / 2;

            query.Transform = new Transform2D(0, areaPosition);
            query.Shape = selectionRect;

            query.CollideWithAreas = true;
            query.CollideWithBodies = false;

            IEnumerable<Selectable> selected = space.IntersectShape(query)
                .SelectMany(v => v.Values)
                .Select(v => v.Obj)
                .OfType<Selectable>();

            foreach (var selectable in selected)
                selectable.Selected(true);
        }
    }
}
