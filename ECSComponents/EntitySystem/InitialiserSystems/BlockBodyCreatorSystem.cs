using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public sealed class BlockBodySystem : BaseCreatorSystem
    {
        private ArchetypeQuery<ElementEcs, BlockEcs, RectEcs> withRectQuery = null!;
        private ArchetypeQuery<ElementEcs, BlockEcs> blockOnlyQuery = null!;
        protected override void OnAddStore(EntityStore store)
        {
            withRectQuery = store.Query<ElementEcs, BlockEcs, RectEcs>();
            withRectQuery.Filter.AnyTags(Tags.Get<UnInitialized>());
            blockOnlyQuery = store.Query<ElementEcs, BlockEcs>();
            blockOnlyQuery.Filter.AnyTags(Tags.Get<UnInitialized>());
        }

        protected override void OnUpdate()
        {
            withRectQuery.Each(new BlockBodyRectCreator());
        }
    }

    internal readonly struct BlockBodyRectCreator : IEach<ElementEcs, BlockEcs,RectEcs>
    {
        public void Execute(ref ElementEcs element, ref BlockEcs block, ref RectEcs rect)
        {
            GD.Print("BLOCK CREATED");
            block.Body = PhysicsFactory.CreateBodyRectangle(
                element.Transform, rect.Extents);
        }
    }


}
