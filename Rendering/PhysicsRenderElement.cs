// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Rendering
{
    public record PhysicsRenderElement (Element Element, Rid Canvas, Rid Area = new Rid()) : RenderElement(Element, Canvas, Area)
    {
        public Rid PhysicsArea;

        public override void SetTransforms()
        {
            base.SetTransforms();
            GD.PrintRich("[code][color=orange] Updated");
            PhysicsServer2D.BodySetShapeTransform(PhysicsArea, 0, Element.Transform);
        }
    }
}
