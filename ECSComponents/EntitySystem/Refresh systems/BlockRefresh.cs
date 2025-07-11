// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class BlockRefresh : QuerySystem<ElementEcs, BlockEcs>
    {
        public BlockRefresh() => Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());

        protected override void OnUpdate()
        {
            foreach (var (elements, blocks, entities) in Query.Chunks)
            {
                for (int n = 0; n < entities.Length; n++) {
                    PhysicsServer2D.BodySetShapeTransform(blocks[n].Body, 0, elements[n].Transform);
                }
            }
        }
    }
}
