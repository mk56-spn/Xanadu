// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Factories;
using XanaduProject.IO;

namespace XanaduProject.Scenes.MeshEditor
{
    public static class ItemBuilder
    {

        /// <summary>
        /// A mesh builder that takes an item and creates a rid array from it.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public static RenderRid[] BuildItem(Item item, EntityStore store)
        {
            RenderRid[] rids = new RenderRid[item.MeshLayers.Count];
            int i = 0;
            foreach (var meshLayer in item.MeshLayers)
            {
                MeshComponent m;
                store.CreateEntity(m = meshLayer.MeshComponent);

                var (vertices, indices) = BezierTriangulator.Triangulate(m.BezierPoints);

                m.RenderRid.AddTriangleArray(vertices, indices, m.Color);
                rids[i] = m.RenderRid;
                i++;
            }

            return rids;
        }
    }
}
