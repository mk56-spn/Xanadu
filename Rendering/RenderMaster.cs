// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Tools;
using static Godot.RenderingServer;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Rendering
{
    [Tool]
    public partial class RenderMaster : Control
    {
        private readonly RenderGroup[] groups = new RenderGroup[1000];

        public readonly NoteProcessor NoteProcessor;
        public readonly SerializableStage SerializableStage;
        public readonly TrackHandler TrackHandler;

        public readonly List<RenderElement> RenderElements = [];

        public readonly RenderCharacter RenderCharacter;


        public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
        {
            NoteProcessor = new NoteProcessor(TrackHandler = new TrackHandler(trackInfo), RenderCharacter = new RenderCharacter(TrackHandler));

            AddChild(TrackHandler);
            AddChild(NoteProcessor);


            StaticBody2D staticBody2D = new StaticBody2D{ Position = new Vector2(0, 16)};
            staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D()});
            AddChild(staticBody2D);

            SerializableStage = serializableStage;

            Rid baseCanvas = CanvasItemCreate();
            CanvasItemSetParent(baseCanvas, GetCanvasItem());

            AddChild(RenderCharacter);

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
                            (renderElement as PhysicsRenderElement)!.PhysicsArea = createArea(physicsElement);
                        break;
                    case NoteElement noteElement:
                        renderElement = new RenderElement(element, CreateItem(element));
                        NoteProcessor.Notes.Add(new Note(noteElement, renderElement.Canvas));
                        break;
                    default:
                        renderElement = new RenderElement(element, CreateItem(element));
                        break;
                }

                RenderElements.Add(renderElement);
            }
        }

        public override void _Ready()=>
            TrackHandler.StartTrack();

        public override void _ExitTree() {
            foreach (var element in RenderElements)
                element.Remove();
        }

        public override void _Draw()
        {
            base._Draw();
            DrawLine(new Vector2(-10000, 16), new Vector2(10000, 16), XanaduColors.XanaduPink, 3);
        }

        protected Rid CreateItem(Element element)
        {
            Rid canvas;
            CanvasItemSetParent(canvas = CanvasItemCreate(), groups[element.Group].Rid);
            CanvasItemSetTransform(canvas, element.Transform);
            CanvasItemSetModulate(canvas, element.Colour);
            CanvasItemSetZIndex(canvas, element.Zindex);

            Rect2 rect = new Rect2(-element.Size() / 2, element.Size());


            switch (element)
            {
                case PhysicsElement physicsElement:
                    Color[] c = new Color[4];
                    Array.Fill(c, Colors.White);
                    Vector2[] v = [
                        -Vector2.One * physicsElement.Size() / 2,
                        new Vector2(1, -1) * physicsElement.Size() / 2,
                        Vector2.One * physicsElement.Size() / 2,
                        new Vector2(-1, 1) * physicsElement.Size() / 2];


                    CanvasItemAddPrimitive(canvas,(ReadOnlySpan<Vector2>)v, c, [], default);
                    break;
                case NoteElement:

                    CanvasItemAddCircle(canvas, Vector2.Zero, NoteElement.RADIUS, Colors.White);
                    CanvasItemAddLine(canvas, new Vector2(-10,0), new Vector2(10,0), Colors.Red);
                    break;
                case TextElement textElement:
                    var size = ThemeDB.FallbackFont.GetStringSize(textElement.Text, fontSize: textElement.TextSize);
                    ThemeDB.FallbackFont.DrawString(canvas, new Vector2(-size.X, size.Y / 2) / 2, textElement.Text, fontSize: textElement.TextSize);
                    break;
                case TextureElement textureElement:
                    CanvasItemAddTextureRect(canvas, rect, SerializableStage.DynamicTextures[textureElement.Texture].GetRid());
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

        public Texture[] GetTextures() => SerializableStage.DynamicTextures;

        public int ChildCount() => RenderElements.Count;
    }
}
