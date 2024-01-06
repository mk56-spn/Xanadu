// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.GD;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
    [Tool]
    public partial class RenderMaster (SerializableStage serializableStage) : Node2D
    {
        private readonly RenderGroup[] groups = new RenderGroup[1000];
        private (Rid, Element)[] rids = Array.Empty<(Rid, Element)>();

        public override void _Ready()
        {
            base._Ready();

            Rid baseCanvas = CanvasItemCreate();
            CanvasItemSetParent(baseCanvas, GetCanvasItem());

            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new RenderGroup();
                CanvasItemSetParent(groups[i].Rid, baseCanvas);
            }

            createElements();
        }

        private void createElements()
        {

            rids = new (Rid, Element)[serializableStage.Elements.Length];
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < serializableStage.Elements.Length; i++)
            {
                var element = serializableStage.Elements[i];
                Rid canvas;
                CanvasItemSetParent(canvas = CanvasItemCreate(), groups[element.Group].Rid);
                CanvasItemSetTransform(canvas, new Transform2D(Mathf.DegToRad(element.Rotation), Vector2.One * Randf(), element.Skew, element.Position));
                rids[i] = (canvas, element);

                switch (serializableStage.Elements[i])
                {
                    case TextElement textElement:
                        break;
                    default:
                        CanvasItemAddTextureRect(canvas, new Rect2(serializableStage.Elements[i].Position, new Vector2(100, 50)), serializableStage.DynamicTextures[0].GetRid());
                        break;
                }
            }
        }
    }
}
