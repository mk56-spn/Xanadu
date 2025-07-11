// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
	public partial class RenderMaster : Control
	{
		public readonly EntityStore EntityStore;
		public readonly RenderCharacter RenderCharacter;
		private Root root;
		public Rid BaseCanvas;

		public Clock Clock = new(new TrackInfo
		{
			SongTitle = "Heavens's Fall",
			Track = "res://Resources/Helblinde - Heaven_s Fall.ogg",
			TimingPoints = [(3, 20), (5, 200)]

		});

		public RenderMaster(SerializableStage serializableStage, TrackInfo trackInfo)
		{
			AddChild(Clock);
			GlobalClock.Register(Clock);
			EntityStore = serializableStage.EntityStore;

			var staticBody2D = new StaticBody2D { Position = new Vector2(0, 1000) };
			staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D() });

			AddChild(staticBody2D);
			AddChild(RenderCharacter = new RenderCharacter(this));
			BaseCanvas = CanvasItemCreate();
			CanvasItemSetParent(BaseCanvas, GetCanvasItem());
			AddChild(GD.Load<PackedScene>("uid://cx1jpfb2mwknx").Instantiate());

		}



		public override void _Process(double delta)
		{
			root.Update(new UpdateTick());
		}

		public override void _Ready()
		{
			root = new Root(EntityStore, RenderCharacter, this);
		}
	}
}
