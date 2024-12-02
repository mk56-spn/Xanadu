// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Audio;

namespace XanaduProject.Rendering
{
	[Tool]
	public partial class RenderCharacter : CharacterBody2D
	{
		private CollisionShape2D collisionShape2D;
		private readonly TrackHandler trackHandler;
		private bool reset;
		public RenderCharacter(TrackHandler trackHandler)
		{
			this.trackHandler = trackHandler;

			SetCollisionLayer(0b00000000_00000000_00000000_00001101);
			SetCollisionMask(0b00000000_00000000_00000000_00001101);

			collisionShape2D = new CollisionShape2D();

			AddChild(collisionShape2D);
			collisionShape2D.DebugColor = Colors.Yellow with { A = 0.5f };
			collisionShape2D.Shape = new CapsuleShape2D { Radius = 32, Height = 128f};

			MotionMode = MotionModeEnum.Grounded;


			trackHandler.Stopped += () => Position = Vector2.Zero;


        }


		public override void _PhysicsProcess(double delta)
		{
			collisionShape2D.Disabled = !trackHandler.Playing;


			Velocity = Velocity with { Y = (float)Mathf.Min(Velocity.Y + 980 * 2 * delta, 980) };

			MoveAndSlide();
		}

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, 10, Colors.Purple );
		}
	}
}
