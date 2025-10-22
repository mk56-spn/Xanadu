// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.AssetCreation.ItemEditor;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Screens.AssetCreation.Skeleton;
using XanaduProject.Singleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class ItemDisplaySystem(bool meshRecalculating = false) : QuerySystem<RootEcs>
    {
        private readonly Container container = PoseServices.GetViewer();

        private readonly StandardPoseSkin skin = SkinIndex.GetAllSkins().First().Value;
        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);

            Logger.AddLog(LogCategory.General, "ItemDisplaySystem");

            var buffer = store.GetCommandBuffer();
            store.Query<RootEcs>().ForEachEntity(((ref RootEcs root, Entity entity) =>
            {
                root.Canvas.SetParent(container);

                foreach (var link in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    CanvasEcs canvasEcs = new CanvasEcs();
                    canvasEcs.Canvas.SetParent(root.Canvas).SetZIndex(-10);
                    buffer.AddComponent(link.Entity.Id, canvasEcs);
                }
            } ));
            buffer.Playback();

            updateSkin(true);

        }


        private void updateSkin(bool recalc)
        {
            int i = 0;

            DiProvider.Get<EntityStore>().Query<RootEcs>().ForEachEntity(((ref RootEcs root, Entity entity) =>
            {
                root.Canvas.SetTransform(new Transform2D(0, container.Size / 2));

                foreach (var link in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    if (!link.Entity.TryGetComponent(out NameEcs name)) continue;

                    var canvas = link.Entity.GetComponent<CanvasEcs>();
                    canvas.Canvas.SetTransform(new Transform2D(link.Component.GlobalAngle -  float.Pi / 2, link.Component.GlobalPosition));

                    Item? item = skin.ItemAssignments.Where(c => c.Key == name.Name).Select(c=> c.Value).FirstOrDefault();;
                    if (item == null) continue;

                    if (!recalc) continue;

                    canvas.Canvas.Clear();

                    canvas.Canvas.AddString(Vector2.Zero, name.Name, 10);

                    item.BuildItemOnCanvas(canvas.Canvas, Colors.White.Darkened(0.1f * i));

                    i++;
                }
            } ));
        }
        protected override void OnUpdate()
        {
            updateSkin(meshRecalculating);
        }
    }
}
