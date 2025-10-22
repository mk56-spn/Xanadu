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
        public static void BuildItemOnCanvas(this Item item, RenderRid canvas, Color color)
        {
            foreach (var component in item.Components)
            {
                if (component is not SerializableMeshComponent serializableMesh) continue;

                var (vertices, indices) = BezierTriangulator.Triangulate(serializableMesh.MeshComponent.BezierPoints);

                if (vertices.Count < 3 || indices.Count <= 0) return;

                canvas.AddTriangleArray(vertices, indices, color);
            }
        }
    }
}
