// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Rendering;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class MainRefreshSystem : BaseRefreshSystem
    {
        private EntityStore entityStore = null!;

        #region Queries

        private ArchetypeQuery<ElementEcs, BlockMaterialEcs> queryMaterials = null!;
        private ArchetypeQuery<ElementEcs, RectEcs> queryRect = null!;

        #endregion

        protected override void OnAddStore(EntityStore store)
        {
            entityStore = store;


            queryMaterials = store.Query<ElementEcs, BlockMaterialEcs>();
            queryMaterials.Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());

            queryRect = store.Query<ElementEcs, RectEcs>();
            queryRect.Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());

        }

        protected override void OnUpdate()
        {
            Filter.AnyTags(Tags.Get<SelectionFlag, Dormant, UnInitialized>());


            //Order is important as refreshElement purges the canvas

            Query.EachEntity(new RefreshElement());
            queryRect.EachEntity(new RefreshRect());

            Query.EachEntity(new Refresh(entityStore));

            queryMaterials.ForEachEntity((ref ElementEcs elementEcs, ref BlockMaterialEcs blockMaterial, Entity _) =>
                CanvasItemSetMaterial(elementEcs.Canvas, BlockMaterials.Get(blockMaterial.Shader)));
        }
    }

    internal readonly struct RefreshElement : IEachEntity<ElementEcs>
    {
        public void Execute(ref ElementEcs elementEcs, int id)
        {
            CanvasItemClear(elementEcs.Canvas);

            CanvasItemSetZIndex(elementEcs.Canvas, elementEcs.Index);
            CanvasItemSetTransform(elementEcs.Canvas, elementEcs.Transform);
        }
    }

    internal readonly struct RefreshRect : IEachEntity<ElementEcs, RectEcs>
    {
        public void Execute(ref ElementEcs element, ref RectEcs rect, int id)
        {
            CanvasItemAddRect(element.Canvas, new Rect2(-rect.Extents / 2, rect.Extents), Colors.White);
        }
    }

    internal readonly struct Refresh(EntityStore entityStore) : IEachEntity<ElementEcs>
    {
        public void Execute(ref ElementEcs elementEcs, int id)
        {
            var entity = entityStore.GetEntityById(id);
            @try<SelectionEcs>(entity, elementEcs);
            @try<HitZoneEcs>(entity, elementEcs);
            @try<HurtZoneEcs>(entity, elementEcs);
        }

        private void @try<T>(Entity entity, ElementEcs elementEcs) where T : struct, IComponent, IUpdatable
        {
            if (entity.TryGetComponent<T>(out var t))
                t.Update(elementEcs);
        }
    }
}
