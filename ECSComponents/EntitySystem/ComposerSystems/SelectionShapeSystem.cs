// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class SelectionShapeSystem : ComposerSystem
    {
        private readonly IVisualsMaster master = DiProvider.Get<IVisualsMaster>();
        private readonly RenderRid canvasItem = RenderRid.Create();

        private static readonly Color composer_colour = Colors.Red;
        private static readonly Color composer_colour_dim = composer_colour with { A =  0.1F};

        public SelectionShapeSystem()
        {
            canvasItem.SetParent(master.GameplayerLayerRid)
                .SetZIndex(2);
        }
        protected override void OnUpdate()
        {
            canvasItem.Clear();

            Composer.Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity entity) =>
            {
                canvasItem.AddSetTransform(element.Transform);

                if (entity.HasComponent<NoteEcs>())
                {
                    canvasItem.AddCircle(NoteEcs.RADIUS, color: composer_colour_dim)
                        .AddCircleOutline(NoteEcs.RADIUS, color: composer_colour);
                    return;
                }

                if (entity.TryGetComponent(out RectEcs rect))
                {
                    canvasItem
                        .AddRect(rect.Extents, composer_colour_dim)
                        .AddRectOutline(rect.Extents, composer_colour);
                }
            });
        }
    }
}
