// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class TileSystem : QuerySystem
    {
        private readonly RenderRid canvas = RenderRid.Create();

        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);
            canvas.SetParent(GameServices.Canvas.GetCanvas());

        }

        protected override void OnUpdate()
        {

        }
    }
}
