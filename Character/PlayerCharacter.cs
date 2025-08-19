// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Character
{
    public partial class PlayerCharacter : CharacterBody2D, IPlayerCharacter
    {
        private readonly IClock clock = DiProvider.Get<IClock>();
        public RenderRid PlayerCanvasRid { get; }

        internal Entity Entity;

        private readonly PhaseMachine phaseMachine;
        public MotionMachine MotionMachine { get; }

        public static readonly float CHARACTER_HEIGHT = 128;

        public PlayerCharacter()
        {
            phaseMachine = new PhaseMachine(this);
            MotionMachine = new MotionMachine(this);

            PlayerCanvasRid = GetCanvasItem().AsRenderRid();
            AddChild(new CharacterDamage());
            AddChild(new CharacterVisuals(this));

            createShape();

            MotionMode = MotionModeEnum.Grounded;

            clock.Stopped += () =>
            {
                Position = Velocity = new Vector2(0, 0);
                Position = new Vector2(0, 0);

                if (MotionMachine.State == MovementState.Holding)
                {
                    MotionMachine.Fire(Trigger.HoldTimeout);
                    inertiaTimeLeft = freezeTimeLeft = 0;

                }
            };
            Position = new Vector2(0, 0);
        }


        public override void _Process(double delta)
        {
            base._Process(delta);

            if (Input.IsActionJustReleased(new StringName("main")))
                ReleaseHold();
        }

        public override void _EnterTree()
        {
            Entity =  DiProvider.Get<EntityStore>().CreateEntity();
            Entity.AddComponent<CharacterEcs>();
        }

        private CollisionShape2D collisionShape2D = null!;
        private void createShape()
        {
            SetCollisionLayer(PhysicsFactory.PLAYER_AREA_FLAG);
            SetCollisionMask(PhysicsFactory.PLAYER_AREA_FLAG);


            collisionShape2D = new CollisionShape2D();

            AddChild(collisionShape2D);
            collisionShape2D.Shape = new CapsuleShape2D { Radius = 31, Height = 128f };
        }

        public const float MAX_RUN_SPEED = 600f;
        private const float gravity = 2500f;
        private const float inertia_duration = 0.4f;
        private const float stopping_friction = 5000f;

        private float inertiaTimeLeft; //
        private float freezeTimeLeft; //


        public void TriggerHold(float seconds)
        {
            MotionMachine.Fire(MotionMachine.StartHoldTrigger, seconds);
        }

        public void ReleaseHold()
        {
            if (MotionMachine.State is MovementState.Holding or MovementState.MovingAndHolding)
                MotionMachine.Fire(Trigger.HoldTimeout);
        }

        public void TriggerDirectedAcceleration(Direction direction)
        {
            MotionMachine.Fire(MotionMachine.AccelerateTrigger, direction);
        }

        public void OnAccelerate(Direction direction)
        {
            // Preserve the components that are not explicitly affected
            // by the chosen direction.
            float targetX = Velocity.X;
            float targetY = Velocity.Y;

            switch (direction)
            {
                case Direction.Left:
                    targetX = -MAX_RUN_SPEED;
                    break;

                case Direction.Right:
                    targetX =  MAX_RUN_SPEED;
                    break;

                case Direction.Up:
                    targetY = -MAX_RUN_SPEED;
                    break;

                case Direction.Down:
                    targetY =  MAX_RUN_SPEED;
                    break;

                case Direction.UpLeft:
                    targetX = -MAX_RUN_SPEED;
                    targetY = -MAX_RUN_SPEED;
                    break;

                case Direction.UpRight:
                    targetX =  MAX_RUN_SPEED;
                    targetY = -MAX_RUN_SPEED;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }

            // Apply the new velocity vector.
            Velocity = new Vector2(targetX, targetY);

            // Restart inertia so both components can naturally decay
            // through the logic in _PhysicsProcess.
            inertiaTimeLeft = inertia_duration;
        }

        public void OnStartHold(float seconds)
        {
            freezeTimeLeft = Mathf.Max(freezeTimeLeft, seconds);
        }

        public void EndHold()
        {
            freezeTimeLeft = 0;
        }

        public void SnapToPosition(Vector2 worldPosition)
        {
            GlobalPosition = worldPosition;
        }


        public override void _PhysicsProcess(double delta)
        {
            if (clock.IsPaused) return;

            float dt = (float)delta;

            updateCharacterContactPhase();

            // 1. Update timers and fire timeout triggers
            if (inertiaTimeLeft > 0)
            {
                inertiaTimeLeft -= dt;
                if (inertiaTimeLeft <= 0) MotionMachine.Fire(Trigger.InertiaTimeout);
            }

            if (freezeTimeLeft > 0)
            {
                freezeTimeLeft -= dt;

                if (freezeTimeLeft <= 0) MotionMachine.Fire(Trigger.HoldTimeout);
            }

            // 2. Apply physics based on state
            float horizontalVelocity = calculateHorizontalVelocity(dt);
            float verticalVelocity = calculateVerticalVelocity(dt);


            Velocity = new Vector2(horizontalVelocity, verticalVelocity);

            MoveAndSlide();
            updateEntityCharacter();
        }

        private void updateCharacterContactPhase()
        {
            bool onFloorNow = IsOnFloor();

            switch (phaseMachine.State)
            {
                case Phase.Grounded when !onFloorNow:
                    phaseMachine.Fire(PhaseTrigger.LeaveGround);
                    break;
                case Phase.Airborne when onFloorNow:
                    phaseMachine.Fire(PhaseTrigger.Land);
                    break;

            }
        }

        private void updateEntityCharacter()
        {
            ref var v = ref Entity.GetComponent<CharacterEcs>();
            v.Position = Position;
            v.Velocity = Velocity;
                }

        private float calculateVerticalVelocity(float dt)
        {
            float verticalVelocity = Velocity.Y;

            if (freezeTimeLeft > 0)
                return 0;

            if (phaseMachine.State == Phase.Airborne || MotionMachine.State is MovementState.Idle or MovementState.Moving)
            {
                // Standard gravity while airborne or in normal motion
                verticalVelocity = Velocity.Y + gravity * dt;
            }


            return verticalVelocity;
        }

        private float calculateHorizontalVelocity(float dt)
        {
            float horizontalVelocity = Velocity.X;
            if (MotionMachine.State is MovementState.Idle or MovementState.Holding
                && phaseMachine.State == Phase.Grounded)
            {
                // Apply stopping friction only when grounded
                horizontalVelocity = Mathf.MoveToward(Velocity.X, 0, stopping_friction * dt);
            }

            return horizontalVelocity;
        }
    }
}
