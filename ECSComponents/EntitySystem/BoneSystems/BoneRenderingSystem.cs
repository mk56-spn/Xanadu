// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.EcGui;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EcGuiSetup;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.Singleton;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneRenderingSystem :QuerySystem<RootEcs>
    {
        protected override void OnAddStore(EntityStore store)
        {

                EcGui.AddExplorerStore("Store", store);
                TypeDrawers.Register();
                EcGui.AddExplorerSystems(SystemRoot);


                EcGui.Explorer.AddQuery("Bones",store.Query<BoneEcs>());
                EcGui.Explorer.AddComponentMemberColumn<BoneGlobalTransform>(nameof(BoneGlobalTransform.GlobalAngle));
                EcGui.Explorer.AddComponentMemberColumn<BoneEcs>(nameof(BoneEcs.Angle));
                EcGui.Explorer.AddComponentMemberColumn<BoneEcs>(nameof(BoneEcs.Length));
        }

        private int i = 0;
        protected override void OnUpdate()
        {
            i++;
            if (i % 100 == 0)
            {
                GD.Print(Query.Count);

            }

            /*
            EcGui.ExplorerWindow();
            EcGui.InspectorWindow();
            Query.ForEachEntity((ref RootEcs component1, Entity entity)  =>
            {

                component1.Canvas.Clear();
                foreach (var v in entity.GetIncomingLinks<BoneGlobalTransform>())
                {

                    Logger.AddLog(LogCategory.Animation,entity.GetIncomingLinks<BoneGlobalTransform>().ToString());
                    ref BoneGlobalTransform boneGlobalTransform = ref v.Component;
                    component1.Canvas.AddCircle(5, position: boneGlobalTransform.GlobalPosition);
                }
            });*/
        }
    }
}
