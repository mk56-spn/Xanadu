// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class ScalingSystem : QuerySystem
    {
        private readonly AnimatedSlider slider = new()
        {

        };
        private readonly IComposer composer = DiProvider.Get<IComposer>();


        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);
            composer.ComposerUiCanvas.AddChild(slider);
            slider.CustomMinimumSize = new Vector2(300, 30);

            slider.ValueChanged += value =>
            {
                composer.Selected.ForEachEntity((
                    (ref ElementEcs element, ref SelectionEcs _, Entity _) =>
                    {
                        element.Transform = new Transform2D(0, new Vector2((float)value, (float)value),
                            element.Transform.Skew, element.Transform.Origin);
                    }));
            };
        }


        protected override void OnUpdate()
        {
            bool visible = composer.Selected.Count == 1;

            slider.Visible = visible;

            if (!visible) return;

            var camera = GodotTree.Tree.Root.GetCamera2D();


            Vector2 position = composer.Selected.Entities.Single().GetComponent<ElementEcs>().Origin;

            var transformedPos = (position - camera.Offset ) * camera.Zoom;

            slider.Position = transformedPos - slider.Size / 2 + GodotTree.Tree.Root.Size / 2 - new Vector2(0, 100);
        }
    }
}
