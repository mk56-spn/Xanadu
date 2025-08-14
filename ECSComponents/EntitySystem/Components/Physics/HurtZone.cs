// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.Colors;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct HurtZoneEcs : IComponent, IUpdatable
    {
        public static readonly Vector2[] TRIANGLE =
        [
            new(-16, 16),
            new(0, -16),
            new(16, 16)
        ];

        [Ignore] public Rid Area;


        public void Update(ElementEcs elementEcs)
        {
            AreaSetTransform(Area, elementEcs.Transform);
            RenderingServer.CanvasItemAddPolygon(elementEcs.Canvas, TRIANGLE,
                [
                    White.Darkened(0.5f),
                    White,
                    White.Darkened(0.5f)
                ]
            );
        }
    }
}
