// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents.Tag
{
    public struct HoldEcs : IComponent, IUpdatable
    {
        [Composer("HoldDirection")] public bool HoldDirection;

        public float Duration = 0.6f;

        public HoldEcs()
        {
        }

        public void Update(ElementEcs elementEcs)
        {
            ThemeDB.FallbackFont.DrawString(elementEcs.Canvas, Vector2.Zero, "ORDER");
            RenderingServer.CanvasItemAddLine(elementEcs.Canvas, Vector2.Zero,
                new Vector2(Duration * 750f - 10, 0), Colors.Red);
            RenderingServer.CanvasItemAddCircle(elementEcs.Canvas,
                new Vector2(Duration * 750f, 0), 10, Colors.Red);
        }
    }
}
