// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents
{
    public struct PolygonEcs : IComponent
    {
        public required Vector2[] Points;
        public required Color[] Colors;

        public readonly struct Create : IEach<PolygonEcs, ElementEcs>
        {
            public void Execute(ref PolygonEcs polygonEcs, ref ElementEcs element)
            {
                GD.PrintRich("[code][color=purple]Line canvas called");
                RenderingServer.CanvasItemAddPolygon(element.Canvas, polygonEcs.Points, polygonEcs.Colors.Reverse().ToArray());
               RenderingServer.CanvasItemAddPolyline(element.Canvas, polygonEcs.Points, polygonEcs.Colors);
            }
        }
    }
}
