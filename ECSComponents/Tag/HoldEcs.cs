// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents.Tag
{
    public struct HoldEcs() : IComponent
    {
        public float Duration = 0.6f;


    }
}
