// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Factories;
using XanaduProject.IO;

namespace XanaduProject.Screens
{
    [ComponentKey(null)]
    public struct CanvasEcs() : IComponent
    {
        public RenderRid Canvas = RenderRid.Create();
    }

    [ComponentKey(null)]
    public struct ItemEcs() : IComponent
    {
        public Item Item;
    }
}
