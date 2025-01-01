// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents
{
    public struct RectEcs() : IComponent, IUpdatable
    {
        [Composer("Filled")]
        public bool Filled = true;

        public required Vector2 Extents;

        [Composer("Line width")]
        // ReSharper disable once MemberCanBePrivate.Global
        public int LineWidth = 1;


        public static readonly Vector2[] PRESETS =
        [
            new(32, 32),
            new(64, 64),
            new(128, 128),
            new(128, 32),
            new(256, 256),

        ];
        public static readonly Vector2 SMALL = new(16, 16);
        public static readonly Vector2 MEDIUM = new(32, 32);
        public static readonly Vector2 LARGE = new(64, 64);

        public void Update( ElementEcs element)
        {
            if (Filled)
                RenderingServer.CanvasItemAddRect(element.Canvas, new Rect2(-Extents / 2, Extents), Colors.White);

            Vector2 adjustedExtents = Extents - new Vector2(LineWidth, LineWidth);
            Vector2 topLeft = - adjustedExtents / 2;
            Vector2 topRight = adjustedExtents / 2 * new Vector2(1,-1);
            Vector2 bottomRight = adjustedExtents / 2;
            Vector2 bottomLeft = adjustedExtents / 2 * new Vector2(-1,1);
            RenderingServer.CanvasItemAddPolyline(element.Canvas,
                [topLeft,topRight,bottomRight,bottomLeft, topLeft],
                [], LineWidth);
        }
    }
}
