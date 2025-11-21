// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using Godot;
using Xanadu.Singletons;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Singleton;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class DuplicationSystem : QuerySystem<ElementEcs>
    {
        protected override void OnAddStore(EntityStore store)
        {
            Filter.AllTags(Tags.Get<SelectionFlag>());
        }

        private readonly IVisualsMaster master = DiProvider.Get<IVisualsMaster>();

        private bool lPressedLastFrame;

        protected override void OnUpdate()
        {
            bool lJustPressed = Input.IsKeyPressed(Key.L) && !lPressedLastFrame;
            lPressedLastFrame = Input.IsKeyPressed(Key.L);


            if (!lJustPressed) return;

            Query.ForEachEntity(((ref ElementEcs component1, Entity entity) =>
            {
                Logger.AddLog(LogCategory.General, $"Cloning {entity.Id}");
                var cloneEntity =  entity.CloneEntity();

                CommandBuffer.AddTag<UnInitialized>(cloneEntity.Id);
                CommandBuffer.RemoveTag<SelectionFlag>(entity.Id);


                ref var cloneElement = ref cloneEntity.GetComponent<ElementEcs>();
                cloneElement.Transform = cloneElement.Transform.Translated(new Vector2(10, 10));

                cloneElement.Canvas.Rid = RenderRid.Create(master.GameplayerLayerRid);
            } ));

            CommandBuffer.Playback();
        }
    }
}
