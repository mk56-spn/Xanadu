// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class ItemDisplaySystem(Control control, StandardPoseSkin skin) : QuerySystem<RootEcs>
    {
        private readonly RenderRid canvas = RenderRid.Create(control);


        protected override void OnUpdate()
        {
            canvas.Clear();

            int i = 0;
            Query.ForEachEntity(((ref RootEcs root, Entity entity) =>
            {
                foreach (var link in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    if (!link.Entity.TryGetComponent(out NameEcs name)) continue;

                    canvas.AddSetTransform(new Transform2D(link.Component.GlobalAngle -  float.Pi / 2, link.Component.GlobalPosition));
                    canvas.AddString(Vector2.Zero, name.Name, 10);

                    Item? item = skin.ItemAssignments.Where(c => c.Key == name.Name).Select(c=> c.Value).FirstOrDefault();;
                    if (item == null) continue;


                    foreach (var component in item.Components)
                    {
                        if (component is not SerializableMeshComponent serializableMesh) continue;

                        var (vertices, indices) = BezierTriangulator.Triangulate(serializableMesh.MeshComponent.BezierPoints);

                        if (vertices.Count < 3 || indices.Count <= 0) return;

                        canvas.AddTriangleArray(vertices, indices, Colors.White.Darkened(0.1f * i));
                    }

                    i++;
                }
            } ));
        }
    }
}
