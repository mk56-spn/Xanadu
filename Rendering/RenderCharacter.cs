// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;
using static XanaduProject.Rendering.Materials;

namespace XanaduProject.Rendering
{
	public partial class RenderCharacter : CharacterBody2D
	{
		private readonly RenderMaster renderMaster;
		private CollisionShape2D collisionShape2D = null!;

		private Entity? entity;

		private bool floor;

		private Vector2 heldPosition = Vector2.Zero;


		private bool last;
		private bool reset;
		private Node2D visuals;


		public RenderCharacter(RenderMaster renderMaster)
		{
			this.renderMaster = renderMaster;
			visuals = GD.Load<PackedScene>("uid://c0artk3e0k6yt").Instantiate<Node2D>();
			AddChild(visuals);

			createShape();

			MotionMode = MotionModeEnum.Grounded;

			renderMaster.Clock.Stopped += () =>
			{
				HoldEntity = null;
				Position = Velocity = Vector2.Zero;
			};
			Position = new Vector2(0, 0);

		}

		private Vector2 size = new(800, 800);
		public override void _Process(double delta)
		{
			base._Process(delta);

			if (Input.IsActionJustPressed("R2"))
				setupHitVisuals(NoteType.R2);
	

			if (Input.IsActionJustPressed("R1"))
				setupHitVisuals(NoteType.R1);

		}
		private void setupHitVisuals(NoteType type)
		{
			Rid rid = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetZIndex(rid,30);
			RenderingServer.CanvasItemAddRect(rid, new Rect2(-size /2, size),Colors.White);
			RenderingServer.CanvasItemSetMaterial(rid, Hits.Get(HitMaterialId.Default));
			RenderingServer.CanvasItemSetParent(rid, GetParent<RenderMaster>().GetCanvasItem());
			RenderingServer.CanvasItemSetModulate(rid, type == NoteType.R1 ? Colors.Red : Colors.Red.Darkened(0.1f));
			RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "hit_pos", GlobalClock.Instance.PlaybackTimeSec);
			cleanup(rid);
		}


		private async void cleanup(Rid rid)
		{
			double timeAtHit = GlobalClock.Instance.PlaybackTimeSec;

			while (GlobalClock.Instance.PlaybackTimeSec <= timeAtHit + 0.7)
				await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);

			RenderingServer.FreeRid(rid);
		}

		public Entity? HoldEntity
		{
			set
			{
				entity = value;
				heldPosition = Position;
			}
		}

		private void createShape()
		{
			SetCollisionLayer(BlockEcs.COLLISION_FLAG);
			SetCollisionMask(BlockEcs.COLLISION_FLAG);

			collisionShape2D = new CollisionShape2D();

			AddChild(collisionShape2D);
			collisionShape2D.Shape = new CapsuleShape2D { Radius = 31, Height = 128f };
		}

		public override void _PhysicsProcess(double delta)
		{
			if (queryDamage())
				renderMaster.Clock.Restart();

			if (GlobalClock.Instance.IsPaused) return;

			Velocity = Velocity with
			{
				Y = (float)Mathf.Min(Velocity.Y + 2000 * 2 * delta, 2000)
			};

			visuals.Scale = visuals.Scale with { X = Velocity.X < 0 ? -1 : 1 };

			MoveAndSlide();
		}

		private bool queryDamage()
		{
			var query = new PhysicsShapeQueryParameters2D
			{
				Transform = GlobalTransform,
				Shape = new CircleShape2D { Radius = 32 },
				CollideWithAreas = true,
				CollisionMask = 0b_00000000_10000000_00000000_00000000
			};

			return GetWorld2D().DirectSpaceState
				.IntersectShape(query).Count != 0;
		}
	}
}
