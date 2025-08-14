// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class RotationSystem : ComposerSystem
    {
        private readonly Rotation rotation = new();

        protected override void OnUpdate()
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left)) return;
            if (Composer is { Rotating: true , State: ComposerInput.InputState.Idle})
                Query.Each(rotation);
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
