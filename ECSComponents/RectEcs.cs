// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents
{
    public struct RectEcs() : IComponent
    {
        public bool Filled = false;
        public required Vector2 Extents;

        public readonly struct Create : IEach<RectEcs, ElementEcs>
        {
            public void Execute(ref RectEcs rectEcs, ref ElementEcs element)
            {
                GD.PrintRich("[code][color=pink]Rect canvas called");

                if (rectEcs.Filled)
                    RenderingServer.CanvasItemAddRect(element.Canvas, new Rect2(-rectEcs.Extents / 2, rectEcs.Extents), Colors.White);

                Vector2 topLeft = - rectEcs.Extents / 2;
                Vector2 topRight = rectEcs.Extents / 2 * new Vector2(1,-1);
                Vector2 bottomRight = rectEcs.Extents / 2;
                Vector2 bottomLeft = rectEcs.Extents / 2 * new Vector2(-1,1);
                RenderingServer.CanvasItemAddPolyline(element.Canvas,
                    [topLeft,topRight,bottomRight,bottomLeft, topLeft],
                    [], 3);
            }
        }
    }
}
