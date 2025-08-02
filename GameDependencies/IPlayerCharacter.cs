// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Godot;
using XanaduProject.ECSComponents;

namespace XanaduProject.GameDependencies
{
    public interface IPlayerCharacter
    {
        public Vector2 Position { get; }

        public void TriggerDirectedAcceleration(Direction direction);
        public void SnapToPosition(Vector2 worldPosition);
    }
}
