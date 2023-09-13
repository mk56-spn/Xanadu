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

        private Vector2 dragStart;
        private Vector2 dragEnd;
        private RectangleShape2D selectionRect = new RectangleShape2D();
        private bool dragging;

        [Dependency]
        private Camera2D camera => DependOn<Camera2D>();

        public SelectionArea ()
        {
            ZIndex = 2;
        }

        public override void _Draw()
        {
            base._Draw();

            if (!dragging) return;

            Vector2 size = dragEnd - dragStart;
            DrawRect(new Rect2(dragStart, size), Colors.IndianRed, false, 2);
            DrawRect(new Rect2(dragStart, size), Colors.IndianRed with { A = 0.3f });

            DrawString(new FontFile(), new Vector2(10, 20), dragging.ToString().ToUpper(), modulate: Colors.GreenYellow);
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.Shift)) return;

            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse:

                    dragging = true;
                    dragStart =  mouse.Position;
                    dragEnd = dragStart;
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                    dragging = false;
                    updateSelected = !updateSelected;
                    break;

                case InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouse:
                    dragEnd = mouse.GlobalPosition;

                    break;

                default:
                    return;
            }
            QueueRedraw();
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!updateSelected) return;

            updateSelected = !updateSelected;
            selectItems();
        }

        private void selectItems()
        {
            var space = GetWorld2D().DirectSpaceState;
            var query = new PhysicsShapeQueryParameters2D();

            if (dragEnd.X < dragStart.X)
                (dragEnd.X, dragStart.X) = (dragStart.X, dragEnd.X);

            if (dragEnd.Y < dragStart.Y)
                (dragEnd.Y, dragStart.Y) = (dragStart.Y, dragEnd.Y);

            selectionRect.Size = dragEnd - dragStart;

            Vector2 areaPosition = camera.Offset + dragStart + (dragEnd - dragStart) / 2;

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
