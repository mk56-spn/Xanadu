// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class TempValueApplySystem : QuerySystem<TempValuesEcs, ElementEcs>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref TempValuesEcs component1, ref ElementEcs elementEcs, Entity entity) =>
            {
                elementEcs.UpdateScale(component1.Transform2D.Scale);

                component1.Transform2D = Transform2D.Identity;
            });
        }
    }
}
