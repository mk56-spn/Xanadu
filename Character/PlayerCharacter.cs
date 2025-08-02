// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using Stateless;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;

namespace XanaduProject.Character
{
    public partial class PlayerCharacter : CharacterBody2D, IPlayerCharacter
    {
        private readonly IClock clock = DiProvider.Get<IClock>();

        //------------------------------------------------------------------
        // State Machine
        //------------------------------------------------------------------
        private enum State { Idle, Moving, Holding, MovingAndHolding }
        private enum Trigger { Accelerate, StartHold, InertiaTimeout, HoldTimeout }

        private readonly StateMachine<State, Trigger> stateMachine;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<Direction> accelerateTrigger;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<float> startHoldTrigger;


        public PlayerCharacter()
        {
            AddChild(new CharacterDamage());
            AddChild(new CharacterVisuals(this));

            createShape();

            MotionMode = MotionModeEnum.Grounded;

            //------------------------------------------------------------------
            // State Machine Configuration
            //------------------------------------------------------------------
            stateMachine = new StateMachine<State, Trigger>(State.Idle);
            accelerateTrigger = stateMachine.SetTriggerParameters<Direction>(Trigger.Accelerate);
            startHoldTrigger = stateMachine.SetTriggerParameters<float>(Trigger.StartHold);

            stateMachine.Configure(State.Idle)
                .Permit(Trigger.Accelerate, State.Moving)
                .Permit(Trigger.StartHold, State.Holding);

            stateMachine.Configure(State.Moving)
                .OnEntryFrom(accelerateTrigger, OnAccelerate)
                .PermitReentry(Trigger.Accelerate)
                .Permit(Trigger.InertiaTimeout, State.Idle)
                .Permit(Trigger.StartHold, State.MovingAndHolding);

            stateMachine.Configure(State.Holding)
                .OnEntryFrom(startHoldTrigger, OnStartHold)
                .OnExit(EndHold)
                .Permit(Trigger.HoldTimeout, State.Idle)
                .Permit(Trigger.Accelerate, State.MovingAndHolding);

            stateMachine.Configure(State.MovingAndHolding)
                .OnEntryFrom(startHoldTrigger, OnStartHold)
                .OnEntryFrom(accelerateTrigger, OnAccelerate)
                .OnExit(EndHold)
                .Permit(Trigger.InertiaTimeout, State.Holding)
                .Permit(Trigger.HoldTimeout, State.Moving)
                .PermitReentry(Trigger.Accelerate);


            clock.Stopped += () =>
            {
                Position = Velocity = new Vector2(1000, 0);
                Position = new Vector2(0, 0);
            };
            Position = new Vector2(0, 0);
        }


        private CollisionShape2D collisionShape2D;
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
        public void TriggerDirectedAcceleration(Direction direction)
        {
            stateMachine.Fire(accelerateTrigger, direction);
        }

        public void StartHold(float seconds)
        {
            stateMachine.Fire(startHoldTrigger, seconds);
        }

        //------------------------------------------------------------------
        //  State Machine Actions -------------------------------------------
        //------------------------------------------------------------------

        private void OnAccelerate(Direction direction)
        {
            float targetSpeed = 0;
            switch (direction)
            {
                case Direction.Left:
                    targetSpeed = -max_run_speed;
                    break;
                case Direction.Right:
                    targetSpeed = max_run_speed;
                    break;
                case Direction.Up:
                    return;
            }

            Velocity = Velocity with { X = targetSpeed };
            inertiaTimeLeft = inertia_duration;
        }

        private void OnStartHold(float seconds)
        {
            freezeTimeLeft = Mathf.Max(freezeTimeLeft, seconds);
            Velocity = Velocity with { Y = 0 };
        }

        private void EndHold()
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

            // 1. Update timers and fire timeout triggers
            if (inertiaTimeLeft > 0)
            {
                inertiaTimeLeft -= dt;
                if (inertiaTimeLeft <= 0) stateMachine.Fire(Trigger.InertiaTimeout);
            }

            if (freezeTimeLeft > 0)
            {
                freezeTimeLeft -= dt;
                if (freezeTimeLeft <= 0) stateMachine.Fire(Trigger.HoldTimeout);
            }

            // 2. Apply physics based on state
            float horizontalVelocity = Velocity.X;
            if (stateMachine.State == State.Idle || stateMachine.State == State.Holding)
            {
                horizontalVelocity = Mathf.MoveToward(Velocity.X, 0, stopping_friction * dt);
            }

            float verticalVelocity = Velocity.Y;
            if (stateMachine.State == State.Idle || stateMachine.State == State.Moving)
            {
                verticalVelocity = Velocity.Y + gravity * dt;
            }

            Velocity = new Vector2(horizontalVelocity, verticalVelocity);

            // 3. Move
            MoveAndSlide();
        }
    }
}
