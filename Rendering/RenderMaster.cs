// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
	public partial class RenderMaster : Control
	{
		private NoteProcessor noteProcessor;
		public readonly TrackHandler TrackHandler;
		public readonly EntityStore EntityStore;
		private readonly RenderCharacter renderCharacter;

		public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
		{
			EntityStore = serializableStage.EntityStore;
			TrackHandler = new TrackHandler(trackInfo);
			renderCharacter = new RenderCharacter(TrackHandler);
			noteProcessor = new NoteProcessor(TrackHandler, renderCharacter, EntityStore);

			AddChild(TrackHandler);
			AddChild(noteProcessor);

			StaticBody2D staticBody2D = new StaticBody2D { Position = new Vector2(0, 1000)};
			staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D()});

			AddChild(staticBody2D);
			AddChild(renderCharacter);

			Rid baseCanvas = CanvasItemCreate();
			CanvasItemSetParent(baseCanvas, GetCanvasItem());

			EntityStore.Query<ElementEcs>().Each(new ElementEcs.CreateEach(baseCanvas));
		}

		public override void _EnterTree()
		{
			EntityStore.Query<RectEcs, ElementEcs>().Each(new RectEcs.Create());
			EntityStore.Query<PolygonEcs, ElementEcs>().Each(new PolygonEcs.Create());
			EntityStore.Query<RectEcs, ElementEcs, BlockEcs>().Each(new BlockEcs.Create(this));
			EntityStore.Query<NoteEcs, ElementEcs>().ForEachEntity(
				(ref NoteEcs _, ref ElementEcs elementEcs, Entity entity) =>
				{
					entity.AddComponent(new HitZoneEcs
					{
						Area = HitZoneEcs.CreateAreaRound(elementEcs, GetWorld2D())
					});
				});

			EntityStore.Query<NoteEcs,ElementEcs>().Each(new NoteEcs.CreateNote());
		}

		public override void _Ready()
		{
			TrackHandler.StartTrack();
		}
	}
}
