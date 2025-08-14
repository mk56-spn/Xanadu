// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using Stateless;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Character
{
    public partial class PlayerCharacter : CharacterBody2D, IPlayerCharacter
    {
        private readonly IClock clock = DiProvider.Get<IClock>();

        private Entity entity;
        private enum Phase { Grounded, Airborne }
        private enum PhaseTrigger { LeaveGround, Land }

        private readonly StateMachine<Phase, PhaseTrigger> phaseMachine;

        //------------------------------------------------------------------
        // MovementState Machine
        //------------------------------------------------------------------

        private readonly StateMachine<MovementState, Trigger>.TriggerWithParameters<Direction> accelerateTrigger;
        private readonly StateMachine<MovementState, Trigger>.TriggerWithParameters<float> startHoldTrigger;


        public PlayerCharacter()
        {
            AddChild(new CharacterDamage());
            AddChild(new CharacterVisuals(this));

            createShape();

            MotionMode = MotionModeEnum.Grounded;

            //------------------------------------------------------------------
            // MovementState Machine Configuration
            //------------------------------------------------------------------
            StateMachine = new StateMachine<MovementState, Trigger>(MovementState.Idle);
            accelerateTrigger = StateMachine.SetTriggerParameters<Direction>(Trigger.Accelerate);
            startHoldTrigger = StateMachine.SetTriggerParameters<float>(Trigger.StartHold);

            StateMachine.Configure(MovementState.Idle)
                .Permit(Trigger.Accelerate, MovementState.Moving)
                .Permit(Trigger.StartHold, MovementState.Holding);

            StateMachine.Configure(MovementState.Moving)
                .OnEntryFrom(accelerateTrigger, OnAccelerate)
                .PermitReentry(Trigger.Accelerate)
                .Permit(Trigger.InertiaTimeout, MovementState.Idle)
                .Permit(Trigger.StartHold, MovementState.MovingAndHolding);

            StateMachine.Configure(MovementState.Holding)
                .OnEntryFrom(startHoldTrigger, OnStartHold)
                .Permit(Trigger.HoldTimeout, MovementState.Idle)
                .Permit(Trigger.Accelerate, MovementState.MovingAndHolding);

            StateMachine.Configure(MovementState.MovingAndHolding)
                .OnEntryFrom(startHoldTrigger, OnStartHold)
                .OnEntryFrom(accelerateTrigger, OnAccelerate)
                .Permit(Trigger.InertiaTimeout, MovementState.Holding)
                .Permit(Trigger.HoldTimeout, MovementState.Moving)
                .PermitReentry(Trigger.Accelerate);

            StateMachine.OnTransitioned(transition =>
            {
                if (transition.Trigger == Trigger.HoldTimeout)
                {
                    endHold();
                }
            });

            phaseMachine = new StateMachine<Phase, PhaseTrigger>(Phase.Grounded);

            Grounded g = new Grounded();

            phaseMachine.Configure(Phase.Grounded)
                .OnEntry(()=>
                {
                    GD.Print("ground");
                    entity.EmitSignal(g);
                })
                .Permit(PhaseTrigger.LeaveGround, Phase.Airborne);

            phaseMachine.Configure(Phase.Airborne)
                .OnEntry(()=>
                {
                    GD.Print("ground");
                    entity.EmitSignal(new Airborne());
                })
                .Permit(PhaseTrigger.Land, Phase.Grounded);


            clock.Stopped += () =>
            {
                Position = Velocity = new Vector2(0, 0);
                Position = new Vector2(0, 0);

                if (StateMachine.State == MovementState.Holding)
                {
                    StateMachine.Fire(Trigger.HoldTimeout);
                    inertiaTimeLeft = freezeTimeLeft = 0;

                }
            };
            Position = new Vector2(0, 0);
        }

        public override void _EnterTree()
        {
            base._EnterTree();
            entity =  DiProvider.Get<EntityStore>().CreateEntity();
            entity.AddComponent<CharacterEcs>();
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


        //------------------------------------------------------------------
        // Tunables
        //------------------------------------------------------------------
        private const float max_run_speed = 600f;
        private const float gravity = 2500f;
        private const float inertia_duration = 0.4f;
        private const float stopping_friction = 5000f;

        //------------------------------------------------------------------
        // Runtime state driven by note systems
        //------------------------------------------------------------------
        private float inertiaTimeLeft; //
        private float freezeTimeLeft; //

        //------------------------------------------------------------------
        //  PUBLIC  API  ----------------------------------------------------
        //------------------------------------------------------------------

        public StateMachine<MovementState, Trigger> StateMachine { get; private set; }

        public void TriggerHold(float seconds)
        {
            StateMachine.Fire(startHoldTrigger, seconds);
        }

        public void TriggerDirectedAcceleration(Direction direction)
        {
            StateMachine.Fire(accelerateTrigger, direction);
        }

        //------------------------------------------------------------------
        //  MovementState Machine Actions -------------------------------------------
        //------------------------------------------------------------------

        private void OnAccelerate(Direction direction)
        {
            // Preserve the components that are not explicitly affected
            // by the chosen direction.
            float targetX = Velocity.X;
            float targetY = Velocity.Y;

            switch (direction)
            {
                case Direction.Left:
                    targetX = -max_run_speed;
                    break;

                case Direction.Right:
                    targetX =  max_run_speed;
                    break;

                case Direction.Up:
                    targetY = -max_run_speed;          // Negative Y = up in Godot
                    break;

                case Direction.Down:
                    targetY =  max_run_speed;          // Positive Y = down
                    break;

                case Direction.UpLeft:
                    targetX = -max_run_speed;
                    targetY = -max_run_speed;
                    break;

                case Direction.UpRight:
                    targetX =  max_run_speed;
                    targetY = -max_run_speed;
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

        private void OnStartHold(float seconds)
        {
            freezeTimeLeft = Mathf.Max(freezeTimeLeft, seconds);
        }

        private void endHold()
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
                if (inertiaTimeLeft <= 0) StateMachine.Fire(Trigger.InertiaTimeout);
            }

            if (freezeTimeLeft > 0)
            {
                freezeTimeLeft -= dt;

                if (freezeTimeLeft <= 0) StateMachine.Fire(Trigger.HoldTimeout);
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
            ref var v = ref entity.GetComponent<CharacterEcs>();
            v.Position = Position;
            v.Velocity = Velocity;
        }

        private float calculateVerticalVelocity(float dt)
        {
            float verticalVelocity = Velocity.Y;

            if (freezeTimeLeft > 0)
                return 0;

            if (phaseMachine.State == Phase.Airborne || StateMachine.State is MovementState.Idle or MovementState.Moving)
            {
                // Standard gravity while airborne or in normal motion
                verticalVelocity = Velocity.Y + gravity * dt;
            }


            return verticalVelocity;
        }

        private float calculateHorizontalVelocity(float dt)
        {
            float horizontalVelocity = Velocity.X;
            if (StateMachine.State is MovementState.Idle or MovementState.Holding
                && phaseMachine.State == Phase.Grounded)
            {
                // Apply stopping friction only when grounded
                horizontalVelocity = Mathf.MoveToward(Velocity.X, 0, stopping_friction * dt);
            }

            return horizontalVelocity;
        }
    }
    public enum MovementState { Idle, Moving, Holding, MovingAndHolding }

    public enum Trigger { Accelerate, StartHold, InertiaTimeout, HoldTimeout }
}
