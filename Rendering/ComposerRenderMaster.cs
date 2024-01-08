// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.PhysicsServer2D;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
    public partial class ComposerRenderMaster  : RenderMaster
    {
        public readonly Dictionary<Rid, RenderInfo> Dictionary = new Dictionary<Rid, RenderInfo>();

        private List<(Rid, Vector2)> selectedAreas = [];

        private bool held;
        private Vector2 heldMousePosition;

        public ComposerRenderMaster(SerializableStage serializableStage) : base(serializableStage)
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
        }
        public override void _EnterTree()
        {
            base._EnterTree();

            foreach (var renderInfo in RenderElements)
                Dictionary.Add(createArea(renderInfo.Element), renderInfo);
        }


        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) return;

            if (selectedAreas.Count == 0) return;

            foreach (var selectedArea in selectedAreas)
                removeElement(selectedArea.Item1);

            selectedAreas = [];
        }

        public override void _GuiInput(InputEvent @event)
        {

            base._GuiInput(@event);

            QueueRedraw();

            if (@event is not InputEventMouse) return;

            if (held)
                foreach (var area in selectedAreas)
                    moveElement(area.Item1, area.Item2);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;

            if (!held & @event.IsPressed())
                heldMousePosition = GetLocalMousePosition();

            held = @event.IsPressed();

            if (!@event.IsPressed()) return;

            selectPoint();
        }

        private void selectPoint()
        {
            selectedAreas = [];

            foreach (var rid in queryPoint())
                selectedAreas.Add((rid, Dictionary[rid].Element.Position));

            if (selectedAreas.Count == 0)
                addElement();
        }

        private Rid[] queryPoint()
        {
            var query = new PhysicsPointQueryParameters2D
            {
                Position = GetLocalMousePosition(),
                CollideWithAreas = true,
                CollideWithBodies = false,
            };

            return GetWorld2D().DirectSpaceState
                .IntersectPoint(query)
                .SelectMany(v => v.Values)
                .Select(c => c.Obj)
                .OfType<Rid>().ToArray();
        }

        private void moveElement(Rid area, Vector2 position)
        {
            Rid canvas = Dictionary[area].Canvas;
            Element element = Dictionary[area].Element;
            element.Position = position + (GetLocalMousePosition() - heldMousePosition);
            CanvasItemSetTransform(canvas,  element.GetElementTransform());
            AreaSetTransform(area, element.GetElementTransform());
        }

        #region ElementCreation

        private void addElement()
        {
            GD.Print("Item added");
            Element element = new Element
            {
                Group = 1,
                Position = GetLocalMousePosition(),
                Rotation = 70,
                Scale = new Vector2(1, 1)
            };

            Rid canvas = CreateItem(element);
            Rid area = createArea(element);
            Dictionary.Add(area, new RenderInfo(canvas, element));

            QueueRedraw();
        }

        private Rid createArea(Element element)
        {
            Rid area = AreaCreate();
            Rid shape = RectangleShapeCreate();

            Transform2D transform = element.GetElementTransform();

            AreaSetSpace(area, GetWorld2D().Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape, element.GetSize() / 2);

            AreaSetCollisionLayer(area, 1);

            AreaSetTransform(area, transform);
            AreaSetMonitorable(area, true);

            return area;
        }

        private void removeElement(Rid area)
        {
            PhysicsServer2D.FreeRid(area);
            RenderingServer.FreeRid(Dictionary[area].Canvas);

            Dictionary.Remove(area);
            QueueRedraw();
        }

        #endregion

        public override void _Draw()
        {
            Vector2 center = Vector2.Zero;

            foreach (var element in Dictionary)
                DrawCircle(element.Value.Element.Position, 20, Colors.Red);

            foreach (var rid in selectedAreas)
            {
                Element element = Dictionary[rid.Item1].Element;
                center += element.Position;
                DrawSetTransformMatrix(element.GetElementTransform());
                DrawRect(new Rect2(-element.GetSize() / 2, element.GetSize()), Colors.Green with { A = 0.5f });
            }

            DrawSetTransformMatrix(Transform2D.Identity);
            DrawCircle(center / selectedAreas.Count, 10, Colors.Olive);
        }
    }
}
