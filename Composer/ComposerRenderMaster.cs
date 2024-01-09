// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Rendering;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.PhysicsServer2D;
using static Godot.RenderingServer;

namespace XanaduProject.Composer
{
    public partial class ComposerRenderMaster  : RenderMaster
    {
        public readonly Dictionary<Rid, RenderInfo> Dictionary = new Dictionary<Rid, RenderInfo>();

        public List<(Rid, Vector2)> SelectedAreas = [];

        private bool held;
        private Vector2 heldMousePosition;

        private ComposerEditWidget composerScaleWidget;

        public ComposerRenderMaster(SerializableStage serializableStage) : base(serializableStage)
        {
            composerScaleWidget = ComposerEditWidget.Create(this);
            AddChild(composerScaleWidget);

            SetAnchorsPreset(LayoutPreset.FullRect);
        }
        public override void _EnterTree()
        {
            base._EnterTree();

            foreach (var renderInfo in RenderElements)
                Dictionary.Add(createArea(renderInfo.Element), renderInfo);

            MouseFilter = MouseFilterEnum.Pass;
        }

        #region Input handling

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            GD.Print(@event);
            QueueRedraw();

            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            {
                if (SelectedAreas.Count == 0) return;

                foreach (var selectedArea in SelectedAreas)
                    removeElement(selectedArea.Item1);

                SelectedAreas = [];
            }

            if (@event is not InputEventMouse) return;

            if (held)
                foreach (var area in SelectedAreas)
                    moveElement(area.Item1, area.Item2);

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;

            if (!held & @event.IsPressed())
                heldMousePosition = GetLocalMousePosition();

            held = @event.IsPressed();

            if (!@event.IsPressed()) return;

            selectPoint();
        }

        #endregion

        #region ElementModification

        public void ScaleElement(Rid area, Vector2 scale)
        {
            GD.Print("Called");
            Element element = Dictionary[area].Element;
            element.Scale = scale;
            setTransforms(area, element);
        }

        private void moveElement(Rid area, Vector2 position)
        {
            Element element = Dictionary[area].Element;
            element.Position = position + (GetLocalMousePosition() - heldMousePosition);
            setTransforms(area, element);
        }

        public void RotateElement(Rid area, float rotation)
        {
            Element element = Dictionary[area].Element;
            element.Rotation = rotation;
            setTransforms(area, element);
        }

        public void TintElement(Rid area, Color colour)
        {
            Element element = Dictionary[area].Element;
            element.Colour = colour;
            CanvasItemSetModulate(Dictionary[area].Canvas, colour);
        }

        private void setTransforms(Rid area, Element element)
        {
            CanvasItemSetTransform( Dictionary[area].Canvas,  element.Transform());
            AreaSetTransform(area, element.Transform());
        }

        #endregion

        #region ElementCreation

        private void addElement()
        {
            GD.Print("Item added");
            Element element = new TextureElement
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

            Transform2D transform = element.Transform();

            AreaSetSpace(area, GetWorld2D().Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape, element.Size() / 2);

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

        private void selectPoint()
        {
            SelectedAreas = [];

            foreach (var rid in queryPoint())
                SelectedAreas.Add((rid, Dictionary[rid].Element.Position));

            composerScaleWidget.Target = SelectedAreas.Count == 1 ? SelectedAreas.First().Item1 : null;

            if (SelectedAreas.Count == 0)
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

        #region Drawing

        public override void _Draw()
        {
            Vector2 center = Vector2.Zero;

            foreach (var rid in SelectedAreas)
            {
                Element element = Dictionary[rid.Item1].Element;
                center += element.Position;

                DrawSetTransformMatrix(element.Transform());

                DrawRect(new Rect2(-element.Size() / 2, element.Size()), Colors.DeepPink with { A = 0.3f });
                DrawRect(new Rect2(-element.Size() / 2, element.Size()), Colors.DeepPink, false, -0.1f);
            }

            DrawSetTransformMatrix(Transform2D.Identity);
        }

        #endregion

        public Element GetElementForArea(Rid area) {
            return Dictionary[area].Element; }
    }
}
