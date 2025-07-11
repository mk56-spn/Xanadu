// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;
using static Godot.PhysicsServer2D;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem
{
	public class InitializerSystem : QuerySystem<ElementEcs>
	{
		private readonly RenderMaster renderMaster;
		private readonly EntityStore store;


		public InitializerSystem(EntityStore store, RenderMaster renderMaster)
		{

			this.store = store;
			this.renderMaster = renderMaster;
			Filter.AnyTags(Tags.Get<UnInitialized>());
		}

		protected override void OnUpdate()
		{

			Query.WithoutAllComponents(default);
			Query.ForEachEntity((ref ElementEcs elementEcs, Entity entity) =>
			{

				elementEcs.Canvas = CanvasItemCreate();

				CanvasItemSetParent(elementEcs.Canvas, renderMaster.BaseCanvas);

				if (!entity.HasComponent<RectEcs>()) return;
				CanvasItemSetMaterial(elementEcs.Canvas, Materials.Blocks.Get(BlockMaterialId.Chamfer));

			});

			Filter.AnyComponents(default);
			Filter.AnyComponents(ComponentTypes.Get<BlockEcs>());
			Query.EachEntity(new Block(renderMaster));

			Filter.AnyComponents(ComponentTypes.Get<NoteEcs>());
			Query.EachEntity(new Notes(CommandBuffer, renderMaster));

			Filter.AnyComponents(ComponentTypes.Get<HurtZoneEcs>());
			Query.EachEntity(new Obstacles(renderMaster));

			Query.Entities.ApplyBatch(new EntityBatch());

			if (renderMaster is ComposerRenderMaster)
			{
				Filter.AnyComponents(ComponentTypes.Get<ElementEcs>());
				Query.EachEntity(new Selection(store, renderMaster, CommandBuffer));
			}

			Filter.AnyComponents(default);

			Query.EachEntity(new Cleanup(CommandBuffer));

			CommandBuffer.Playback();
		}
	}

	internal readonly struct Cleanup(CommandBuffer c) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs c1, int id)
		{
			GD.Print("called");
			c.RemoveTag<UnInitialized>(id);
			c.AddTag<Dormant>(id);
		}
	}

	internal readonly struct Block(RenderMaster renderMaster) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs element, int id)
		{
			Entity entity = renderMaster.EntityStore.GetEntityById(id);
			if (entity.TryGetComponent(out RectEcs rect))
			{
				ref var block = ref renderMaster.EntityStore.GetEntityById(id).GetComponent<BlockEcs>();
				block.Body = createBodyRectangle(element, rect.Extents, renderMaster.GetWorld2D());
			}
		}
		private static Rid createBodyRectangle(ElementEcs element, Vector2 size, World2D world)
		{
			var body = BodyCreate();
			var shape = RectangleShapeCreate();

			BodySetSpace(body, world.Space);
			BodyAddShape(body, shape);
			ShapeSetData(shape, size / 2);

			BodySetCollisionLayer(body, 0b00000000_00000000_00000000_00001101);
			BodySetCollisionMask(body, 0b00000000_00000000_00000000_00001101);
			BodySetShapeTransform(body, 0, element.Transform);
			BodySetMode(body, BodyMode.Static);

			return body;
		}
	}

	internal readonly struct Notes(CommandBuffer c, RenderMaster renderMaster) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs element, int id)
		{
			var entity = renderMaster.EntityStore.GetEntityById(id);

			ref var noteEcs = ref entity.GetComponent<NoteEcs>();

			noteEcs.NoteCanvas = CanvasItemCreate();
			CanvasItemSetParent(noteEcs.NoteCanvas, element.Canvas);
			Materials.Notes.Set(element.Canvas, noteEcs.NoteCanvas);


			var area = AreaCreate();
			var shape = CircleShapeCreate();

			AreaSetSpace(area, renderMaster.GetWorld2D().Space);
			AreaAddShape(area, shape);
			ShapeSetData(shape, 32);

			AreaSetTransform(area, element.Transform);

			AreaSetCollisionLayer(area, HitZoneEcs.NOTE_AREA_FLAG);

			c.AddComponent(id, new HitZoneEcs(area));
			c.AddComponent(id, entity.HasComponent<DirectionEcs>()
				? ParticlesEcs.DirectionalParticleCreate()
				: ParticlesEcs.NormalParticleCreate());
		}
	}

	internal readonly struct Selection(EntityStore store, RenderMaster renderMaster, CommandBuffer buffer) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs element, int id)
		{
			var ent = store.GetEntityById(id);
			var area = createSelectionArea(ent, element, renderMaster.GetWorld2D().Space);
			buffer.AddComponent(id, new SelectionEcs(area));
		}

		private static Rid createSelectionArea(Entity entity, ElementEcs element, Rid space)
		{
			GD.Print("select");
			var area = AreaCreate();

			Rid shape;

			switch (entity)
			{
				case var _ when entity.HasComponent<NoteEcs>():
					GD.PrintRich("[code][color=orange]Circle");
					shape = CircleShapeCreate();
					ShapeSetData(shape, NoteEcs.RADIUS);
					break;

				case var _ when entity.HasComponent<RectEcs>():
					GD.PrintRich("[code][color=yellow]Rectangle");
					shape = RectangleShapeCreate();
					ShapeSetData(shape, entity.GetComponent<RectEcs>().Extents / 2);
					break;

				case var _ when entity.HasComponent<PolygonEcs>():
					shape = ConvexPolygonShapeCreate();
					ShapeSetData(shape, entity.GetComponent<PolygonEcs>().Points);
					break;

				case var _ when entity.HasComponent<HurtZoneEcs>():
					shape = ConvexPolygonShapeCreate();
					ShapeSetData(shape, HurtZoneEcs.TRIANGLE);
					break;

				default:
					shape = CircleShapeCreate();
					ShapeSetData(shape, NoteEcs.RADIUS);
					break;
			}

			AreaAddShape(area, shape);
			AreaSetSpace(area, space);
			AreaSetTransform(area, element.Transform);
			AreaSetCollisionLayer(area, 0b00000000_00000000_00000000_01000000);

			return area;
		}
	}

	internal readonly struct Obstacles(RenderMaster renderMaster) : IEachEntity<ElementEcs>
	{
		public void Execute(ref ElementEcs c1, int id)
		{
			renderMaster.EntityStore.GetEntityById(id).GetComponent<HurtZoneEcs>().Area =
					HurtZoneEcs.CreateAreaRound(renderMaster.GetWorld2D());
		}
	}
}
