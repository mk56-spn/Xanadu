// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Tools;
using static Godot.RenderingServer;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Rendering
{
	[Tool]
	public partial class RenderMaster : Control
	{
		private readonly RenderGroup[] groups = new RenderGroup[1000];

		private NoteProcessor noteProcessor;
		public readonly TrackHandler TrackHandler;

		public readonly EntityStore  EntityStore;

		private readonly RenderCharacter renderCharacter;


		public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
		{
			EntityStore = serializableStage.EntityStore;
			TrackHandler = new TrackHandler(trackInfo);
			renderCharacter = new RenderCharacter(TrackHandler);
			noteProcessor = new NoteProcessor(TrackHandler,renderCharacter, EntityStore);

			AddChild(TrackHandler);
			AddChild(noteProcessor);

			StaticBody2D staticBody2D = new StaticBody2D{ Position = new Vector2(0, 300)};
			/*staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D() });
			staticBody2D.AddChild(new CollisionShape2D { Shape = new RectangleShape2D(){ Size = new Vector2(2000,10)} });*/

			TreeEntered += () =>
			{
				var body = BodyCreate();
				BodySetMode(body,BodyMode.Static );
				var shape = RectangleShapeCreate();
				ShapeSetData(shape, new Vector2(1000, 10));
				BodyAddShape(body, shape);

				BodySetSpace(BodyCreate(), GetWorld2D().GetSpace());

				BodySetState(body, BodyState.Transform, Transform2D.Identity);
			};


			AddChild(staticBody2D);

			Rid baseCanvas = CanvasItemCreate();
			CanvasItemSetParent(baseCanvas, GetCanvasItem());

			AddChild(renderCharacter);

			for (int i = 0; i < groups.Length; i++)
			{
				groups[i] = new RenderGroup();
				CanvasItemSetParent(groups[i].Rid, baseCanvas);
			}

			EntityStore.Query<NoteEcs>().ForEachEntity(((ref NoteEcs component1, Entity entity) =>
					entity.AddComponent<AreaEcs>()));

			EntityStore.Query<ElementEcs>().Each(new ElementEcs.CreateEach(baseCanvas));
			EntityStore.Query<RectEcs, ElementEcs>().Each(new RectEcs.Create());

			EntityStore.Query<PolygonEcs, ElementEcs>().Each(new PolygonEcs.Create());
		}


		public override void _EnterTree()
		{
			base._EnterTree();
            EntityStore.Query<NoteEcs, ElementEcs, AreaEcs>().Each(new NoteEcs.CreateNote(GetWorld2D()));
            EntityStore.Query<RectEcs, ElementEcs, BlockEcs>().Each(new BlockEcs.Create(this));
		}

		public override void _Ready()
		{
			TrackHandler.StartTrack();
		}

		private static Rid createArea(Entity element, ElementEcs elementEcs, World2D world2D)
		{
			Rid area = BodyCreate();
			Rid shape = RectangleShapeCreate();

			Transform2D transform = elementEcs.Transform;

			BodySetSpace(area, world2D.Space);
			BodyAddShape(area, shape);
			ShapeSetData(shape, elementEcs.Size / 2);

			BodySetCollisionLayer(area, 0b00000000_00000000_00000000_00001101);
			BodySetCollisionMask(area, 0b00000000_00000000_00000000_00001101);
			BodySetShapeTransform(area, 0, transform);
			BodySetMode(area, BodyMode.Static);

			return area;
		}

		public Texture[] GetTextures() => [];

		public int ChildCount() => EntityStore.Count;
	}
}
