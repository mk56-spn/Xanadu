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
    public partial class SelectionHandler : Node2D
    {
        public override partial void _Notification(int what);

        [Dependency] private List<Node> selected => DependOn<List<Node>>();

        private bool dragging;
        private Vector2 dragStart;

        public SelectionHandler ()
        {
            ZIndex = 2;
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            QueueRedraw();
        }

        public override void _Draw()
        {
            base._Draw();

            Vector2 size = GetLocalMousePosition() - dragStart;

            if (!dragging) return;

            DrawRect(new Rect2(dragStart, size), Colors.Aqua, false, 2);
            DrawRect(new Rect2(dragStart, size), Colors.Aqua with { A = 0.3f });
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;

            bool isOverlapping = getOverlappedItems().Any();

            switch (mouse.Pressed)
            {
                case true when isOverlapping:
                    var topmostSelectable = getOverlappedItems().First();

                    foreach (var selectable in getOverlappedItems()
                                 .Where(selectable => selectable.ZIndex > topmostSelectable.ZIndex))
                        topmostSelectable = selectable;

                    topmostSelectable.Selected(true);

                    break;

                case true when !isOverlapping:

                    // TODO : expose node in a better fashion from a selectable to avoid this list to list cast.
                    foreach (var node in selected.ToList())
                        node.GetChildren().OfType<Selectable>().First().Selected(false);

                    dragging = true;
                    dragStart = GetLocalMousePosition();
                    break;

                case false when dragging:
                    dragging = false;
                    selectItems();
                    break;
            }
        }

        private List<Selectable> getOverlappedItems()
        {
            var query = new PhysicsPointQueryParameters2D
            {
                Position = GetLocalMousePosition(),
                CollideWithAreas = true,
                CollideWithBodies = false
            };

            return GetWorld2D().DirectSpaceState.IntersectPoint(query)
                .SelectMany(d => d.Values )
                .Select(v => v.Obj)
                .OfType<Selectable>().ToList();
        }

        private void selectItems()
        {
            Rect2 rect = Utils.GetNonNegativeRect(dragStart, GetLocalMousePosition(), true);

            var query = new PhysicsShapeQueryParameters2D
            {
                Transform =  new Transform2D(0,  rect.Position),
                Shape = new RectangleShape2D { Size = rect.Size },
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
