// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.Character
{
    [ComponentKey(null)]
    public struct CharacterEcs() : IComponent {
        public Vector2 Position = new();
        public Vector2 Velocity;
    };

    public struct Grounded;
    public struct Airborne;
}
