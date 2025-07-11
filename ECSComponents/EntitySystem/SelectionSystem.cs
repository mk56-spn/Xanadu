// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class SelectionSystem(EntityStore store) : QuerySystem<ElementEcs>
    {
        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }
    }
}
