// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Rendering
{
    public struct RenderInfo(Rid canvas, Element element)
    {
        public Rid Canvas = canvas;
        public Element Element = element;
    }
}
