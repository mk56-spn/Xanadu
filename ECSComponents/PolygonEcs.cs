// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents
{
    public struct PolygonEcs : IComponent, IUpdatable
    {
        public required Vector2[] Points;

        public static readonly Vector2[] DEFAULT_POINTS =
        [
            new(-50, -50),
            new(50, -50),
            new(50, 50),
            new(-50, 50)
        ];

        public void Update(ElementEcs elementEcs)
        {

            RenderingServer.CanvasItemAddPolygon(elementEcs.Canvas, Points, []);
        }
    }
}
