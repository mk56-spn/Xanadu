// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class RotationSystem : ComposerSystem
    {
        private readonly IComposer composer = DiProvider.Get<IComposer>();

        private readonly Rotation rotation = new();

        private readonly RenderRid canvas = RenderRid.Create();

        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);
            canvas.AddCircleOutline(30, segments: 128)
                .SetParent(GameServices.Canvas.GetCanvas());

            // Add vertical red line
            canvas.AddLine(new Vector2(0, -50), new Vector2(0, 50), Colors.Red, -1)
                .SetParent(GameServices.Canvas.GetCanvas());

            // Add horizontal blue line
            canvas.AddLine(new Vector2(-50, 0), new Vector2(50, 0), Colors.Blue,-1)
                .SetParent(GameServices.Canvas.GetCanvas());
        }

        protected override void OnUpdate()
        {

            if (composer.Selected.Count == 1 && Composer is { Rotating: true , State: BaseInputHandler.InputState.Idle} )
            {
                canvas.SetVisible(true);

                canvas.SetTransform(composer.Selected.Entities.Single().GetComponent<ElementEcs>().Transform);

                if (Input.IsMouseButtonPressed(MouseButton.Left)) return;
                Query.Each(rotation);

            }

            else
            {
                canvas.SetVisible(false);
            }

        }
        private readonly struct Rotation(): IEach<ElementEcs>
        {
            private readonly IComposer composer = DiProvider.Get<IComposer>();

            public void Execute(ref ElementEcs element)
            {
                float rotation = composer.MousePosLocal.AngleToPoint(element.Transform.Origin);
                float rotation2 = (composer.MousePosLocal - composer.RelativeMouseMotion).AngleToPoint(element.Transform.Origin);

               element.Transform =  element.Transform.RotatedLocal(rotation2 -rotation );
            }
        }
    }
}
