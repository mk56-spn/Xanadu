// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Rendering;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
	public class InitializerSystem : QuerySystem<ElementEcs>
	{
		private readonly EntityStore mainStore = DiProvider.Get<EntityStore>();

		public InitializerSystem()
		{
			Filter.AnyTags(Tags.Get<UnInitialized>());
		}

		protected override void OnUpdate()
		{
			Query.ForEachEntity((ref ElementEcs elementEcs, Entity entity) =>
			{
				GD.Print("called");

				CanvasItemSetParent(elementEcs.Canvas, DiProvider.Get<IVisualsMaster>().GameplayerLayerRid);

				if (!entity.HasComponent<RectEcs>()) return;
				CanvasItemSetMaterial(elementEcs.Canvas, Materials.BLOCKS.Get(BlockShaderId.Chamfer));

				if (!entity.HasComponent<TriangleArrayEcs>()) return;
				CanvasItemSetMaterial(elementEcs.Canvas, Materials.BLOCKS.Get(BlockShaderId.Chamfer));
			});

			Filter.AnyComponents(ComponentTypes.Get<ElementEcs>());
			Query.EachEntity(new Selection(mainStore, CommandBuffer));

        }
	}

	internal readonly struct Selection(EntityStore store, CommandBuffer buffer) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs element, int id)
		{
			var ent = store.GetEntityById(id);
			var area = PhysicsFactory.CreateSelectionArea(ent, element );
			buffer.AddComponent(id, new SelectionEcs(area));
		}

	}
}
