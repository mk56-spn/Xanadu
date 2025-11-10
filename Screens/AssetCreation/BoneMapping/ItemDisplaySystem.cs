// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.Screens.AssetCreation.ItemEditor;
using XanaduProject.Singleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class ItemDisplaySystem : QuerySystem<ItemEcs,CanvasEcs>
    {
        public ItemDisplaySystem() =>
            Filter.AllTags(Tags.Get<Dormant>());

        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref ItemEcs itemEcs, ref CanvasEcs canvasEcs, Entity e) =>
            {
                Logger.AddLog(LogCategory.General,"hello");
                canvasEcs.Canvas.Clear();
                var ecs = canvasEcs;
                itemEcs.Item.Components.ForEach(c =>
                {
                    var v = ((IO.Mesh)c).MeshComponent.BezierPoints;
                    var triangulated = BezierTriangulator.Triangulate(v);
                    ecs.Canvas.AddTriangleArray(triangulated.vertices, triangulated.indices);
                });
                CommandBuffer.RemoveTag<Dormant>(e.Id);
            }));
            CommandBuffer.Playback();
        }
    }
}
