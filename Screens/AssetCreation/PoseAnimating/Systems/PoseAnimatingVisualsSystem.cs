// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Systems
{
    public class PoseAnimatingVisualsSystem : QuerySystem
    {
        private readonly EntityStore store = DiProvider.Get<EntityStore>();

        private readonly ArchetypeQuery<IkTargetComponent> targets;

        public PoseAnimatingVisualsSystem(Control control)
        {
            targets = store.Query<IkTargetComponent>();
            canvas = RenderRid.Create(control);
        }

        private readonly RenderRid canvas;

        protected override void OnUpdate()
        {
            canvas.Clear();
            targets.ForEachEntity(((ref IkTargetComponent component1, Entity entity) =>
            {
                component1.TargetPosition = new Vector2(Mathf.Sin(DateTime.Now.Second + entity.Id) * 100, 0);
                canvas.AddCircle(10, component1.TargetPosition, Colors.White.Darkened(0.3f));
                canvas.AddCircle(5, component1.TargetPosition, Colors.White);

            }));
        }
    }
}
