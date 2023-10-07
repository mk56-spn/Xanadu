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

        public override void _Draw()
        {
            base._Draw();

            Vector2 size = GetLocalMousePosition() - dragStart;
            DrawRect(new Rect2(dragStart, size), Colors.Aqua, false, 2);
            DrawRect(new Rect2(dragStart, size), Colors.Aqua with { A = 0.3f });
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;

            bool isOverlapping = getOverlappedItems().Any();

            List<Node> selectables = new List<Node>();

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
                    foreach (var node in selected)
                    {
                        selectables.Add(node);
                    }

                    dragging = true;
                    dragStart = GetLocalMousePosition();
                    break;

                case false when dragging:
                    dragging = false;
                    selectItems();
                    break;
            }

            foreach (var node in selectables)
            {
                selected.Remove(node);
                node.GetChildren().OfType<Selectable>().Single().Selected(false);
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
