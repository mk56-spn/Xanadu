// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using XanaduProject.Factories;
using XanaduProject.IO;
using XanaduProject.Utils;

namespace XanaduProject.Scenes.ItemEditor
{
    public static class ItemBuilder
    {
        /// <summary>
        /// A component builder that takes an item and creates a rid array from its components.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public static RenderRid[] BuildItem(Item item, EntityStore store)
        {
            List<RenderRid> rids = new List<RenderRid>();

            foreach (var component in item.Components)
            {
                if (component is SerializableMeshComponent serializableMesh)
                {
                    MeshComponent meshComponent = serializableMesh.MeshComponent;
                    // Ensure RenderRid is initialized, potentially with a new parent if needed
                    meshComponent.RenderRid = RenderRid.Create(); // Or pass a parent RID if available
                    store.CreateEntity(meshComponent);

                    MeshUtils.UpdateTriangulation(meshComponent);
                    rids.Add(meshComponent.RenderRid);
                }
                // Add handling for other SerializableComponent types here in the future
            }

            return rids.ToArray();
        }
    }
}
