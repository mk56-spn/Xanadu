// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents
{
    public struct RectEcs() : IComponent
    {
        public bool Filled = true;
        public bool Outline = false;
        public required Vector2 Extents;

        public readonly struct Create : IEach<RectEcs, ElementEcs>
        {
            public void Execute(ref RectEcs rectEcs, ref ElementEcs element)
            {
                GD.PrintRich("[code][color=pink]Rect canvas called");

                if (rectEcs.Filled)
                    RenderingServer.CanvasItemAddRect(element.Canvas, new Rect2(-rectEcs.Extents / 2, rectEcs.Extents), Colors.White);

                /*   Rid r = RenderingServer.ShaderCreate();
                   Rid m = RenderingServer.MaterialCreate();
                   RenderingServer.ShaderSetCode(r, GD.Load<Shader>("res://Shaders/oattern.gdshader").GetCode());
                   RenderingServer.MaterialSetShader(m,r);
                   RenderingServer.CanvasItemSetMaterial(element.Canvas, m);*/
            }
        }
    }
}
