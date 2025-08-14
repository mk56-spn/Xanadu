// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class MouseEntityRepresentation : BaseSystem
    {
        private readonly IComposer composer = DiProvider.Get<IComposer>();
        private readonly IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();

        private readonly RenderRid rid;

        public MouseEntityRepresentation()
        {

            rid = RenderRid.Create(visuals.UiLayer.GetCanvas());

            rid.AddRect(new Vector2(40, 40), Colors.Red with { A = 0.3f});
        }
        protected override void OnUpdateGroup()
        {
            rid.SetTransform(new Transform2D(0, composer.MousePosLocal + composer.ViewportSize / 2));
        }
    }
}
