// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Stateless;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Character
{
    public class MotionMachine : StateMachine<MovementState, Trigger>
    {
        public readonly TriggerWithParameters<Direction> AccelerateTrigger;
        public readonly TriggerWithParameters<float> StartHoldTrigger;

        public MotionMachine(PlayerCharacter character) : base(MovementState.Idle)
        {
            AccelerateTrigger = SetTriggerParameters<Direction>(Trigger.Accelerate);
            StartHoldTrigger = SetTriggerParameters<float>(Trigger.StartHold);

            Configure(MovementState.Idle)
                .Permit(Trigger.Accelerate, MovementState.Moving)
                .Permit(Trigger.StartHold, MovementState.Holding);

            Configure(MovementState.Moving)
                .OnEntryFrom(AccelerateTrigger, character.OnAccelerate)
                .PermitReentry(Trigger.Accelerate)
                .Permit(Trigger.InertiaTimeout, MovementState.Idle)
                .Permit(Trigger.StartHold, MovementState.MovingAndHolding);

            Configure(MovementState.Holding)
                .OnEntryFrom(StartHoldTrigger, character.OnStartHold)
                .Permit(Trigger.HoldTimeout, MovementState.Idle)
                .Permit(Trigger.Accelerate, MovementState.MovingAndHolding);

            Configure(MovementState.MovingAndHolding)
                .OnEntryFrom(StartHoldTrigger, character.OnStartHold)
                .OnEntryFrom(AccelerateTrigger, character.OnAccelerate)
                .Permit(Trigger.InertiaTimeout, MovementState.Holding)
                .Permit(Trigger.HoldTimeout, MovementState.Moving)
                .PermitReentry(Trigger.Accelerate);

            OnTransitioned(transition =>
            {
                character.Entity.GetComponent<CharacterEcs>().Movement = transition.Destination;
                if (transition.Trigger == Trigger.HoldTimeout)
                {
                    character.EndHold();
                }
            });
        }
    };
    public enum Trigger { Accelerate, StartHold, InertiaTimeout, HoldTimeout }

    public enum MovementState { Idle, Moving, Holding, MovingAndHolding }
}
