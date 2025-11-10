// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class InitializeVisuals(Control centre, StandardPoseSkin skin) : QuerySystem<BoneGlobalTransform,CanvasEcs>
    {
        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);

            var command  = store.GetCommandBuffer();

            store.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
                rootEcs.Canvas.SetParent(centre);
                rootEcs.Canvas.SetTransform(new Transform2D(0, centre.Size / 2));


                foreach (var link in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    if (!link.Entity.TryGetComponent(out NameEcs name)) continue;

                    var canvas = new CanvasEcs();
                    canvas.Canvas.SetParent(rootEcs.Canvas).SetZIndex(-10);

                    command.AddComponent(link.Entity.Id, canvas);
                    command.AddTag<Dormant>(link.Entity.Id);

                    Item? item = skin.ItemAssignments.Where(c => c.Key == name.Name).Select(c => c.Value).FirstOrDefault();
                    command.AddComponent(link.Entity.Id,new ItemEcs(){ Item = item?? new Item()});
                }
            });
            command.Playback();
        }

        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref BoneGlobalTransform boneGlobalTransform, ref CanvasEcs canvasEcs, Entity entity) =>
            {
                canvasEcs.Canvas.SetTransform(new Transform2D(boneGlobalTransform.GlobalAngle - float.Pi / 2, boneGlobalTransform.GlobalPosition));
            } ));
        }
    }
}
