// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Factories;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Utils;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
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
            List<RenderRid> rids = [];

            foreach (var component in item.Components)
            {
                if (component is not SerializableMeshComponent serializableMesh) continue;

                MeshComponent meshComponent = serializableMesh.MeshComponent;
                meshComponent.RenderRid = RenderRid.Create();
                MeshUtils.UpdateTriangulation(meshComponent);
                meshComponent.RenderRid.SetModulate(Colors.Red);
                rids.Add(meshComponent.RenderRid);
            }

            return rids.ToArray();
        }
    }
}
