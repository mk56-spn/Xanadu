// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.Singleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class ItemDisplaySystem(Control control) : QuerySystem<RootEcs>
    {
        public RenderRid Canvas = RenderRid.Create(control);
        protected override void OnUpdate()
        {
            Canvas.Clear();
            Query.ForEachEntity(((ref RootEcs component1, Entity entity) =>
            {
                foreach (var link in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    if (!link.Entity.TryGetComponent(out NameEcs name)) continue;

                    Canvas.AddSetTransform(new Transform2D(link.Component.GlobalAngle,link.Component.GlobalPosition));
                    Canvas.AddString(Vector2.Zero, name.Name, 10);

                    Logger.AddLog(LogCategory.General, "name found");
                }

            } ));
        }
    }
}
