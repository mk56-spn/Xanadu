// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using MemoryPack;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using IComponent = System.ComponentModel.IComponent;

namespace XanaduProject.IO
{
    [MemoryPackable]
    public partial record StageDataSerializable
    {
        public StageDataSerializable(EntityStore store)
        {
            foreach (var storeEntity in store.Entities)
            {
                var elementEcs = storeEntity.GetComponent<ElementEcs>();
                var note = storeEntity.GetComponent<NoteEcs>();

            }
        }

        [MemoryPackConstructor]
        public StageDataSerializable()
        {

        }
    }


}
