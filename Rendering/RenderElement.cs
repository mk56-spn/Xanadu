// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Rendering
{
    /// <summary>
    /// A wrapper for a <see cref="Element"/> with information to be used during render time;
    /// </summary>
    /// <param name="Element"></param>
    /// <param name="Area"></param>
    public record RenderElement(Element Element, Rid Canvas, Rid Area = default)
    {
        public readonly Element Element = Element;
        public Rid Canvas = Canvas;
        public Rid Area = Area;

        public void Remove()
        {
            RenderingServer.FreeRid(Canvas);

            if (Area != default)
                PhysicsServer2D.FreeRid(Area);
        }

        public void SetDepth(int value)
        {
            Element.Zindex = value;
            RenderingServer.CanvasItemSetZIndex(Canvas, value);
        }

        public void SetSkew(float value)
        {
            Element.Skew = value;
            SetTransforms();
        }

        public void SetScale(Vector2 value)
        {
            Element.Scale = value;
            SetTransforms();
        }

        public void SetPosition(Vector2 value)
        {
            Element.Position = value;
            SetTransforms();
        }

        public void SetRotation(float rotation)
        {
            Element.Rotation = rotation;
            SetTransforms();
        }

        public void SetTint(Color colour)
        {
            Element.Colour = colour;
            RenderingServer.CanvasItemSetModulate(Canvas, colour);
        }

        protected virtual void SetTransforms()
        {

            RenderingServer.CanvasItemSetTransform(Canvas, Element.Transform);
            PhysicsServer2D.AreaSetTransform(Area,  Element.Transform);
        }
    }
}
