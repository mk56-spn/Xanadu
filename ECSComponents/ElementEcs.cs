// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Composer;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents
{
    public struct ElementEcs() : IComponent
    {
        public Transform2D Transform = Transform2D.Identity;

        [Composer]
        public Color Colour = Colors.Olive;

        public int Index = 0;

        [Ignore] public Rid Canvas;

        [Ignore] public Vector2 Size = new(32, 32);


        public void UpdateCanvas()
        {
            CanvasItemSetTransform(Canvas, Transform);
            CanvasItemSetModulate(Canvas, Colour);
        }

        public void SetTransform(Transform2D transform2D)
        {
            Transform = transform2D;
            CanvasItemSetTransform(Canvas, Transform);
        }

        public static Color ComposerColour = Colors.Red;


        public void SetDepth(int value)
        {
            GD.Print("calledS");
            Index = value;
            CanvasItemSetZIndex(Canvas, value);
        }

        public void SetRotation(float rotation)
        {
            Transform = Transform.RotatedLocal(rotation - Transform.Rotation);
            CanvasItemSetTransform(Canvas, Transform);
        }

        public void SetScale(Vector2 value)
        {
            Transform = Transform.ScaledLocal(value - Transform.Scale);
        }

        public readonly struct CreateEach(Rid baseCanvas) : IEach<ElementEcs>
        {
            public void Execute(ref ElementEcs element) {

                CanvasItemSetParent(element.Canvas = CanvasItemCreate(), baseCanvas);
                element.Canvas = element.Canvas;

                CanvasItemSetTransform(element.Canvas, element.Transform);
                CanvasItemSetZIndex(element.Canvas, element.Index);
            }
        }
    }
}
