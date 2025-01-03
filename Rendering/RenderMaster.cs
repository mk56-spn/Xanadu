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
		public readonly RenderCharacter RenderCharacter;
		public Rid BaseCanvas;

		public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
		{
			EntityStore = serializableStage.EntityStore;
			TrackHandler = new TrackHandler(trackInfo);
			RenderCharacter = new RenderCharacter(TrackHandler);
			noteProcessor = new NoteProcessor(TrackHandler, RenderCharacter, EntityStore);

			AddChild(TrackHandler);
			AddChild(noteProcessor);

			StaticBody2D staticBody2D = new StaticBody2D { Position = new Vector2(0, 1000)};
			staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D()});

			AddChild(staticBody2D);
			AddChild(RenderCharacter);
			BaseCanvas = CanvasItemCreate();
			CanvasItemSetParent(BaseCanvas, GetCanvasItem());
			AddChild(GD.Load<PackedScene>("uid://cx1jpfb2mwknx").Instantiate());
		}

		public override void _EnterTree()
		{

			EntityStore.Query<BlockEcs, ElementEcs, RectEcs>().ForEachEntity(
				(ref BlockEcs blockEcs, ref ElementEcs elementEcs, ref RectEcs rectEcs, Entity _) =>
					blockEcs.Create(elementEcs, rectEcs, GetWorld2D()));

			EntityStore.Query<ElementEcs>()
				.ForEachEntity((ref ElementEcs elementEcs, Entity _) =>
					elementEcs.CanvasCreate(BaseCanvas));

			EntityStore.Query<NoteEcs, ElementEcs>().ForEachEntity(
				(ref NoteEcs _, ref ElementEcs elementEcs, Entity entity) =>
					entity.Add(HitZoneEcs.Create(elementEcs, GetWorld2D()), new ParticlesEcs()));

			EntityStore.Query<HurtZoneEcs, ElementEcs>().ForEachEntity((ref HurtZoneEcs hurtZoneEcs, ref ElementEcs elementEcs,  Entity entity) =>
			{
				hurtZoneEcs.Area = HurtZoneEcs.CreateAreaRound(elementEcs, GetWorld2D());
			} );

			int i = 0;
			EntityStore.Query<ElementEcs>().ForEachEntity((ref ElementEcs elementEcs, Entity entity) =>
				elementEcs.Draw(entity));

			GD.Print(i);
		}

		public override void _Ready()
		{
			TrackHandler.StartTrack();
		}
	}
}
