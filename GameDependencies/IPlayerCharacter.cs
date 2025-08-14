// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Godot;
using Stateless;
using XanaduProject.Character;
using XanaduProject.ECSComponents;
using XanaduProject.Factories;

namespace XanaduProject.GameDependencies
{
    public interface IPlayerCharacter
    {
        public Vector2 Position { get; }
        public StateMachine<MovementState, Trigger> StateMachine { get; }

        public void TriggerHold(float seconds);
        public void TriggerDirectedAcceleration(Direction direction);
        public void SnapToPosition(Vector2 worldPosition);
    }
}
