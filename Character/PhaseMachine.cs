// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Stateless;

namespace XanaduProject.Character
{
    public class PhaseMachine : StateMachine<Phase, PhaseTrigger>
    {
        public PhaseMachine(PlayerCharacter character) : base(Phase.Grounded)
        {   Grounded g = new Grounded();

            Configure(Phase.Grounded)
                .OnEntry(()=> character.Entity.EmitSignal(g))
                .Permit(PhaseTrigger.LeaveGround, Phase.Airborne);

            Configure(Phase.Airborne)
                .OnEntry(()=>  character.Entity.EmitSignal(new Airborne()))
                .Permit(PhaseTrigger.Land, Phase.Grounded);


            OnTransitioned(transition =>
                character.Entity.GetComponent<CharacterEcs>().Phase = transition.Destination);
        }
    }

    public enum PhaseTrigger { LeaveGround, Land }

    public enum Phase { Grounded, Airborne }

}
