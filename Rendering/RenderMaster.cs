// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
    [Tool]
    public partial class RenderMaster : Control
    {
        private readonly RenderGroup[] groups = new RenderGroup[1000];
        protected readonly RenderInfo[] RenderElements;

        private readonly SerializableStage serializableStage;

        public RenderMaster(SerializableStage serializableStage)
        {
            this.serializableStage = serializableStage;
            RenderElements = new RenderInfo[this.serializableStage.Elements.Length];

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
                RenderElements[i] = new RenderInfo(CreateItem(element), element);
            }
        }

        protected Rid CreateItem(Element element)
        {
            Rid canvas;
            CanvasItemSetParent(canvas = CanvasItemCreate(), groups[element.Group].Rid);
            CanvasItemSetTransform(canvas, element.GetElementTransform());
            CanvasItemSetModulate(canvas, Colors.White with { A = 0.4f });
            Texture texture = serializableStage.DynamicTextures[0];

            Rect2 rect = new Rect2(-element.GetSize() / 2, element.GetSize());

            switch (element)
            {
                case TextElement textElement:
                    var size = ThemeDB.FallbackFont.GetStringSize(textElement.Text, fontSize: textElement.TextSize);
                    ThemeDB.FallbackFont.DrawString(canvas, new Vector2(-size.X, size.Y / 2) / 2, textElement.Text, fontSize: textElement.TextSize);
                    break;
                default:
                    CanvasItemAddTextureRect(canvas, rect, texture.GetRid());
                    break;
            }

            return canvas;
        }
    }
}
