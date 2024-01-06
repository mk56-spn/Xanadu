// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Rendering
{
    public partial class RenderGroup : Resource
    {
        public Rid Rid = RenderingServer.CanvasItemCreate();
        private Transform2D transform;
        public Transform2D Transform
        {
            get => transform;
            set
            {
                RenderingServer.CanvasItemSetTransform(Rid, value);
                transform = value;
            }
        }
    }
}
