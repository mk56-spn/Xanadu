// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.RenderingServer;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Rendering
{
    [Tool]
    public partial class RenderMaster : Control
    {
        private readonly RenderGroup[] groups = new RenderGroup[1000];

        private readonly NoteProcessor noteProcessor;
        private readonly SerializableStage serializableStage;
        private readonly TrackHandler trackHandler = new TrackHandler();

        public readonly List<RenderElement> RenderElements = new List<RenderElement>();

        public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
        {
            noteProcessor = new NoteProcessor(trackHandler);

            AddChild(trackHandler);
            AddChild(noteProcessor);
            trackHandler.SetTrack(trackInfo);

            this.serializableStage = serializableStage;

            Rid baseCanvas = CanvasItemCreate();
            CanvasItemSetParent(baseCanvas, GetCanvasItem());

            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new RenderGroup();
                CanvasItemSetParent(groups[i].Rid, baseCanvas);
            }
            for (int i = 0; i < serializableStage.Elements.Length; i++)
            {
                Element element = serializableStage.Elements[i];
                RenderElement renderElement;

                switch (element)
                {
                    case PhysicsElement physicsElement:
                        renderElement = new PhysicsRenderElement(element, CreateItem(element));
                        TreeEntered += () =>
                        {
                            (renderElement as PhysicsRenderElement)!.PhysicsArea = createArea(physicsElement);
                        };
                        break;
                    case NoteElement noteElement:
                        renderElement = new RenderElement(element, CreateItem(element));
                        noteProcessor.Notes.Add(new Note(noteElement, renderElement.Canvas));
                        break;
                    default:
                        renderElement = new RenderElement(element, CreateItem(element));
                        break;
                }

                RenderElements.Add(renderElement);
            }
        }

        public override void _Ready()
        {
            base._Ready();
            trackHandler.StartTrack();
        }

        public override void _ExitTree() {
            foreach (var element in RenderElements)
                element.Remove();
        }

        protected Rid CreateItem(Element element)
        {
            Texture texture = serializableStage.DynamicTextures[GD.RandRange(0, serializableStage.DynamicTextures.Length - 1)];

            Rid canvas;
            CanvasItemSetParent(canvas = CanvasItemCreate(), groups[element.Group].Rid);
            CanvasItemSetTransform(canvas, element.Transform);
            CanvasItemSetModulate(canvas, element.Colour);
            CanvasItemSetZIndex(canvas, element.Zindex);

            Rect2 rect = new Rect2(-element.Size() / 2, element.Size());

            switch (element)
            {
                case PhysicsElement:
                    CanvasItemAddPolygon(canvas, [-Vector2.One * 25, new Vector2(1, -1) * 25, Vector2.One * 25, new Vector2(-1, 1) * 25],
                        [Colors.Red, Colors.Black, Colors.Red, Colors.Black]);
                    break;
                case NoteElement:
                    CanvasItemAddCircle(canvas, Vector2.Zero, NoteElement.RADIUS, Colors.White);
                    break;
                case TextElement textElement:
                    var size = ThemeDB.FallbackFont.GetStringSize(textElement.Text, fontSize: textElement.TextSize);
                    ThemeDB.FallbackFont.DrawString(canvas, new Vector2(-size.X, size.Y / 2) / 2, textElement.Text, fontSize: textElement.TextSize);
                    break;
                case TextureElement:
                    CanvasItemAddTextureRect(canvas, rect, texture.GetRid());
                    break;
            }

            return canvas;
        }

        private Rid createArea(Element element)
        {
            Rid area = BodyCreate();
            Rid shape = RectangleShapeCreate();

            Transform2D transform = element.Transform;

            BodySetSpace(area, GetWorld2D().Space);
            BodyAddShape(area, shape);
            ShapeSetData(shape, element.Size() / 2);

            BodySetCollisionLayer(area, 0b00000000_00000000_00000000_00001101);
            BodySetCollisionMask(area, 0b00000000_00000000_00000000_00001101);
            BodySetShapeTransform(area, 0, transform);
            BodySetMode(area, BodyMode.Static);

            return area;
        }

        public Texture[] GetTextures() => serializableStage.DynamicTextures;

        public int ChildCount() => RenderElements.Count;
    }
}
