// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Json.Fliox.Transform.Query.Ops;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Rendering
{
	public partial class RenderCharacter : CharacterBody2D
	{
		private CollisionShape2D collisionShape2D = null!;
		private readonly TrackHandler trackHandler;
		private bool reset;
		private Node2D visuals;

		public RenderCharacter(TrackHandler trackHandler)
		{
			 visuals = GD.Load<PackedScene>("uid://c0artk3e0k6yt").Instantiate<Node2D>();
			AddChild(visuals);
			this.trackHandler = trackHandler;

			createShape();

			MotionMode = MotionModeEnum.Grounded;

			trackHandler.Stopped += () => Position = Velocity = Vector2.Zero;
			Position = new Vector2(0, 0);
		}

		private bool floor;

		private void createShape()
		{
			SetCollisionLayer(BlockEcs.COLLISION_FLAG);
			SetCollisionMask(BlockEcs.COLLISION_FLAG);

			collisionShape2D = new CollisionShape2D();

			AddChild(collisionShape2D);
			collisionShape2D.DebugColor = Colors.Yellow with { A = 0.5f };
			collisionShape2D.Shape = new CapsuleShape2D { Radius = 31, Height = 128f};
		}

		public Rid[] QueryShape()
		{
			var query = new PhysicsShapeQueryParameters2D
			{
				Transform = Transform,
				Shape = new CircleShape2D
				{
					Radius = 32,
				},
				CollideWithAreas = true,
				CollideWithBodies = false,
				CollisionMask =	0b00000000_00000000_10000000_00000000
			};

			return GetWorld2D().DirectSpaceState
				.IntersectShape(query)
				.SelectMany(v => v.Values)
				.Select(c => c.Obj)
				.OfType<Rid>().ToArray();
		}


		private bool last;
		public override void _PhysicsProcess(double delta)
		{
			if (trackHandler.Playing == false) { return; }

			Velocity = Velocity with
			{
				Y = (float)Mathf.Min(Velocity.Y + 980 * 2 * delta, 980),
			};

			visuals.Scale = visuals.Scale with { X = Velocity.X < 0 ? -1 : 1};

			MoveAndSlide();
		}

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, 10, Colors.Purple );
		}
	}
}
